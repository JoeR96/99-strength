using A2S.Application.Commands.ApiTokens;
using A2S.Application.Common;
using A2S.Application.Queries.ApiTokens;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.ApiTokens;

public class ApiTokenCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly IApiTokenRepository _tokens = Substitute.For<IApiTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly LocalClock _clock;

    public ApiTokenCommandHandlerTests()
    {
        _clock = new LocalClock(_time);
        _currentUser.UserId.Returns(Joe.Value);
    }

    private CreateApiTokenCommandHandler CreateHandler() => new(_tokens, _unitOfWork, _currentUser, _clock);

    private RevokeApiTokenCommandHandler RevokeHandler() => new(_tokens, _unitOfWork, _currentUser, _clock);

    [Fact]
    public async Task Create_ReturnsThePlaintextOnce_AndStoresOnlyItsHash()
    {
        ApiToken? stored = null;
        await _tokens.AddAsync(Arg.Do<ApiToken>(t => stored = t), Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(new CreateApiTokenCommand(" Terran CC "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().MatchRegex("^nns_pat_[A-Za-z0-9_-]{43}$");
        result.Value.Name.Should().Be("Terran CC");
        result.Value.Prefix.Should().Be(result.Value.Token[..12]);
        result.Value.CreatedAt.Should().Be(Now);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(Joe);
        stored.Id.Value.Should().Be(result.Value.Id);
        stored.TokenHash.Should().Be(ApiToken.HashOf(result.Value.Token));
        stored.TokenHash.Should().NotContain(result.Value.Token[12..]);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreatedDto_ToString_LeavesTheSecretOut()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, Now);

        var text = DTOs.CreatedApiTokenDto.From(issued).ToString();

        text.Should().Contain("Terran CC");
        text.Should().NotContain(issued.Plaintext[12..]);
    }

    [Fact]
    public async Task Create_WithTenActiveTokens_IsADomainRuleViolation_AndSavesNothing()
    {
        _tokens.CountActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(10);

        var result = await CreateHandler().Handle(new CreateApiTokenCommand("Eleventh"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        result.Error.Should().Contain("at most 10");
        await _tokens.DidNotReceive().AddAsync(Arg.Any<ApiToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithNineActiveTokens_Succeeds()
    {
        _tokens.CountActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(9);

        var result = await CreateHandler().Handle(new CreateApiTokenCommand("Tenth"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await CreateHandler().Handle(new CreateApiTokenCommand("Terran CC"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_RejectsAMissingName(string? name)
    {
        new CreateApiTokenCommandValidator().TestValidate(new CreateApiTokenCommand(name!))
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validator_AcceptsOneToFiftyCharacters_CountedAfterTrimming()
    {
        var validator = new CreateApiTokenCommandValidator();

        validator.TestValidate(new CreateApiTokenCommand("a")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateApiTokenCommand(new string('a', 50))).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateApiTokenCommand($"  {new string('a', 50)}  ")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateApiTokenCommand(new string('a', 51))).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Revoke_StampsTheTokenAndSaves()
    {
        var token = ApiToken.Issue(Joe, "Terran CC", 0, Now).Token;
        _tokens.GetByIdAsync(token.Id, Arg.Any<CancellationToken>()).Returns(token);
        _time.Advance(TimeSpan.FromHours(2));

        var result = await RevokeHandler().Handle(new RevokeApiTokenCommand(token.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        token.RevokedAtUtc.Should().Be(Now.AddHours(2));
        _tokens.Received(1).Update(token);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_Twice_SucceedsAndKeepsTheFirstTime()
    {
        var token = ApiToken.Issue(Joe, "Terran CC", 0, Now).Token;
        _tokens.GetByIdAsync(token.Id, Arg.Any<CancellationToken>()).Returns(token);

        await RevokeHandler().Handle(new RevokeApiTokenCommand(token.Id.Value), CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(1));
        var again = await RevokeHandler().Handle(new RevokeApiTokenCommand(token.Id.Value), CancellationToken.None);

        again.IsSuccess.Should().BeTrue();
        token.RevokedAtUtc.Should().Be(Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_SomeoneElsesToken_IsNotFound_AndLeavesItActive()
    {
        var theirs = ApiToken.Issue(Other, "Theirs", 0, Now).Token;
        _tokens.GetByIdAsync(theirs.Id, Arg.Any<CancellationToken>()).Returns(theirs);

        var result = await RevokeHandler().Handle(new RevokeApiTokenCommand(theirs.Id.Value), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        theirs.IsActive.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_UnknownId_IsNotFound()
    {
        var result = await RevokeHandler().Handle(new RevokeApiTokenCommand(new Guid("11111111-1111-1111-1111-111111111111")), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsTheCallersTokens_WithNoHashOrSecret()
    {
        var active = ApiToken.Issue(Joe, "Terran CC", 0, Now);
        var revoked = ApiToken.Issue(Joe, "Old laptop", 1, Now.AddDays(-3));
        revoked.Token.MarkUsed(Now.AddDays(-2));
        revoked.Token.Revoke(Now.AddDays(-1));
        _tokens.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<ApiToken> { active.Token, revoked.Token });

        var result = await new ListApiTokensQueryHandler(_tokens, _currentUser).Handle(new ListApiTokensQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().BeEquivalentTo(new
        {
            Id = active.Token.Id.Value,
            Name = "Terran CC",
            Prefix = active.Plaintext[..12],
            CreatedAt = Now,
            LastUsedAt = (DateTimeOffset?)null,
            RevokedAt = (DateTimeOffset?)null
        });
        result.Value[1].LastUsedAt.Should().Be(Now.AddDays(-2));
        result.Value[1].RevokedAt.Should().Be(Now.AddDays(-1));
        typeof(DTOs.ApiTokenDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("Id", "Name", "Prefix", "CreatedAt", "LastUsedAt", "RevokedAt");
    }

    [Fact]
    public async Task List_Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await new ListApiTokensQueryHandler(_tokens, _currentUser).Handle(new ListApiTokensQuery(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
