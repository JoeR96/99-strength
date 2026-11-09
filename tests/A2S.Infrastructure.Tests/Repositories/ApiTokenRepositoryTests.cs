using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Personal API token persistence against a database built by the real migrations, so these tests
/// also prove that <c>AddApiTokens</c> creates a table the model can use.
/// </summary>
[Collection("Database")]
public class ApiTokenRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public ApiTokenRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "apitokens");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private A2SDbContext Reopen() => MigratedDatabase.Reopen(_db);

    [Fact]
    public async Task Migration_CreatesTheTable_WithAUniqueHashIndexAndAUserIndex()
    {
        var indexes = await _db.Database
            .SqlQueryRaw<string>("""SELECT indexdef AS "Value" FROM pg_indexes WHERE tablename = 'ApiTokens'""")
            .ToListAsync();

        indexes.Should().Contain(i => i.Contains("UNIQUE") && i.Contains("\"TokenHash\""));
        indexes.Should().Contain(i => !i.Contains("UNIQUE") && i.Contains("(\"UserId\")"));
        (await _db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Token_RoundTrips_AndIsFoundByHash()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        await new ApiTokenRepository(_db).AddAsync(issued.Token);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var repo = new ApiTokenRepository(verify);
        var byHash = await repo.GetByHashAsync(ApiToken.HashOf(issued.Plaintext));
        var byId = await repo.GetByIdAsync(issued.Token.Id);
        var unknown = await repo.GetByHashAsync(ApiToken.HashOf(ApiToken.Issue(Joe, "x", 0, At).Plaintext));

        byHash.Should().NotBeNull();
        byHash!.Should().BeEquivalentTo(issued.Token, o => o.Excluding(t => t.DomainEvents));
        byHash.Matches(issued.Plaintext).Should().BeTrue();
        byId!.Id.Should().Be(issued.Token.Id);
        unknown.Should().BeNull();
    }

    [Fact]
    public async Task OnlyTheHashAndPrefixAreStored_NeverThePlaintext()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        await new ApiTokenRepository(_db).AddAsync(issued.Token);
        await _db.SaveChangesAsync();

        var row = await _db.Database
            .SqlQueryRaw<string>("""SELECT CAST(t AS text) AS "Value" FROM "ApiTokens" t""")
            .SingleAsync();

        row.Should().Contain(issued.Token.TokenHash);
        row.Should().Contain(issued.Token.Prefix);
        row.Should().NotContain(issued.Plaintext[ApiToken.DisplayPrefixLength..]);
    }

    [Fact]
    public async Task LastUsedAndRevoked_ArePersisted()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        await new ApiTokenRepository(_db).AddAsync(issued.Token);
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var repo = new ApiTokenRepository(edit);
            var token = (await repo.GetByHashAsync(issued.Token.TokenHash))!;
            token.MarkUsed(At.AddMinutes(5));
            token.Revoke(At.AddMinutes(10));
            repo.Update(token);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        var loaded = (await new ApiTokenRepository(verify).GetByIdAsync(issued.Token.Id))!;
        loaded.LastUsedAtUtc.Should().Be(At.AddMinutes(5));
        loaded.RevokedAtUtc.Should().Be(At.AddMinutes(10));
        loaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListForUser_IsNewestFirst_AndScopedToTheUser()
    {
        var repo = new ApiTokenRepository(_db);
        await repo.AddAsync(ApiToken.Issue(Joe, "Oldest", 0, At).Token);
        await repo.AddAsync(ApiToken.Issue(Joe, "Newest", 1, At.AddHours(2)).Token);
        await repo.AddAsync(ApiToken.Issue(Joe, "Middle", 2, At.AddHours(1)).Token);
        await repo.AddAsync(ApiToken.Issue(Other, "Theirs", 0, At).Token);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var joes = await new ApiTokenRepository(verify).ListForUserAsync(Joe);

        joes.Select(t => t.Name).Should().Equal("Newest", "Middle", "Oldest");
    }

    [Fact]
    public async Task CountActive_LeavesOutRevokedTokensAndOtherUsers()
    {
        var repo = new ApiTokenRepository(_db);
        var revoked = ApiToken.Issue(Joe, "Revoked", 0, At).Token;
        revoked.Revoke(At.AddHours(1));
        await repo.AddAsync(revoked);
        await repo.AddAsync(ApiToken.Issue(Joe, "Active 1", 0, At).Token);
        await repo.AddAsync(ApiToken.Issue(Joe, "Active 2", 1, At).Token);
        await repo.AddAsync(ApiToken.Issue(Other, "Theirs", 0, At).Token);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new ApiTokenRepository(verify);

        (await verifyRepo.CountActiveForUserAsync(Joe)).Should().Be(2);
        (await verifyRepo.CountActiveForUserAsync(Other)).Should().Be(1);
        (await verifyRepo.CountActiveForUserAsync(new UserId("user_nobody"))).Should().Be(0);
    }

    [Fact]
    public async Task TheSameHashCannotBeStoredTwice()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        await new ApiTokenRepository(_db).AddAsync(issued.Token);
        await _db.SaveChangesAsync();

        var act = () => _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ApiTokens" ("Id", "UserId", "Name", "TokenHash", "Prefix", "CreatedAtUtc")
            VALUES ({new Guid("22222222-2222-2222-2222-222222222222")}, {Other.Value}, {"Copy"}, {issued.Token.TokenHash}, {issued.Token.Prefix}, {At})
            """);

        (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which.SqlState.Should().Be("23505");
    }
}
