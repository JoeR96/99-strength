using A2S.Application.ApiTokens;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.ApiTokens;

public class ApiTokenAuthenticatorTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly IApiTokenRepository _tokens = Substitute.For<IApiTokenRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ApiTokenAuthenticator _authenticator;
    private readonly IssuedApiToken _issued = ApiToken.Issue(Joe, "Terran CC", 0, Now.AddDays(-1));

    public ApiTokenAuthenticatorTests()
    {
        _authenticator = new ApiTokenAuthenticator(_tokens, _users, _unitOfWork, new LocalClock(_time));
        _tokens.GetByHashAsync(_issued.Token.TokenHash, Arg.Any<CancellationToken>()).Returns(_issued.Token);
        _users.GetByIdAsync(Joe, Arg.Any<CancellationToken>()).Returns(User.Create("joe@example.com", "Joe", Joe));
    }

    [Fact]
    public async Task AValidToken_ResolvesToItsUser_AndRecordsTheUse()
    {
        var identity = await _authenticator.AuthenticateAsync(_issued.Plaintext);

        identity.Should().Be(new ApiTokenIdentity(Joe, "joe@example.com", _issued.Token.Id, "Terran CC"));
        _issued.Token.LastUsedAtUtc.Should().Be(Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LastUsed_IsWrittenAtMostOncePerMinute()
    {
        await _authenticator.AuthenticateAsync(_issued.Plaintext);
        _time.Advance(TimeSpan.FromSeconds(20));
        await _authenticator.AuthenticateAsync(_issued.Plaintext);
        _time.Advance(TimeSpan.FromSeconds(20));
        await _authenticator.AuthenticateAsync(_issued.Plaintext);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _issued.Token.LastUsedAtUtc.Should().Be(Now);

        _time.Advance(TimeSpan.FromSeconds(20));
        await _authenticator.AuthenticateAsync(_issued.Plaintext);

        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        _issued.Token.LastUsedAtUtc.Should().Be(Now.AddSeconds(60));
    }

    [Fact]
    public async Task ARevokedToken_IsRejected_AndNotMarkedUsed()
    {
        _issued.Token.Revoke(Now.AddMinutes(-5));

        var identity = await _authenticator.AuthenticateAsync(_issued.Plaintext);

        identity.Should().BeNull();
        _issued.Token.LastUsedAtUtc.Should().BeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownToken_IsRejected()
    {
        var stranger = ApiToken.Issue(Joe, "Never stored", 0, Now).Plaintext;

        var identity = await _authenticator.AuthenticateAsync(stranger);

        identity.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nns_pat_")]
    [InlineData("nns_pat_tooshort")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.e30.signature")]
    public async Task AMalformedToken_IsRejected_WithoutTouchingTheDatabase(string? presented)
    {
        var identity = await _authenticator.AuthenticateAsync(presented);

        identity.Should().BeNull();
        await _tokens.DidNotReceive().GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATokenWhoseStoredHashDoesNotMatch_IsRejected()
    {
        var other = ApiToken.Issue(Joe, "Other", 0, Now);
        _tokens.GetByHashAsync(other.Token.TokenHash, Arg.Any<CancellationToken>()).Returns(_issued.Token);

        var identity = await _authenticator.AuthenticateAsync(other.Plaintext);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task ATokenWhoseUserIsGone_IsRejected()
    {
        _users.GetByIdAsync(Joe, Arg.Any<CancellationToken>()).Returns((User?)null);

        var identity = await _authenticator.AuthenticateAsync(_issued.Plaintext);

        identity.Should().BeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
