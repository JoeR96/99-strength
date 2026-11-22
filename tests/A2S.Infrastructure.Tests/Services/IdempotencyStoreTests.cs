using A2S.Application.Idempotency;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Xunit;

namespace A2S.Infrastructure.Tests.Services;

[Collection("Database")]
public class IdempotencyStoreTests : IAsyncLifetime
{
    private const string Joe = "user_idem_0001";
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private A2SDbContext _db = null!;

    public IdempotencyStoreTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _clock.NowUtc().Returns(Now);
    }

    public async Task InitializeAsync() => _db = await MigratedDatabase.CreateAsync(_fixture, "idem");

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Save_ThenFind_ReturnsTheStoredAnswer_AndASecondSaveKeepsTheFirst()
    {
        var store = new IdempotencyStore(_db, _clock);
        await store.SaveAsync(Joe, "k1", "POST /x", new IdempotentResponse("HASH1", 200, "{\"value\":1}"), CancellationToken.None);
        await store.SaveAsync(Joe, "k1", "POST /x", new IdempotentResponse("HASH2", 200, "{\"value\":2}"), CancellationToken.None);

        var found = await store.FindAsync(Joe, "k1", CancellationToken.None);

        found.Should().Be(new IdempotentResponse("HASH1", 200, "{\"value\":1}"));
        (await store.FindAsync("user_other", "k1", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task EntriesOlderThanSevenDays_AreIgnored_AndPurgedOnTheNextSave()
    {
        var store = new IdempotencyStore(_db, _clock);
        await store.SaveAsync(Joe, "old", "POST /x", new IdempotentResponse("H", 204, null), CancellationToken.None);

        _clock.NowUtc().Returns(Now.AddDays(8));
        var found = await store.FindAsync(Joe, "old", CancellationToken.None);
        await store.SaveAsync(Joe, "old", "POST /x", new IdempotentResponse("H2", 200, "{}"), CancellationToken.None);

        found.Should().BeNull();
        (await _db.IdempotencyRecords.AsNoTracking().CountAsync()).Should().Be(1);
        (await store.FindAsync(Joe, "old", CancellationToken.None))!.RequestHash.Should().Be("H2");
    }

    [Fact]
    public async Task Migration_IsReversible()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddIdempotencyRecords", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        var afterDown = await TableExistsAsync();
        await migrator.MigrateAsync(ours);
        var afterUp = await TableExistsAsync();

        afterDown.Should().BeFalse();
        afterUp.Should().BeTrue();
    }

    private async Task<bool> TableExistsAsync()
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"IdempotencyRecords\"') IS NOT NULL";
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
