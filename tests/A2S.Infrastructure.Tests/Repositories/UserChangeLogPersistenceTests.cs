using A2S.Domain.Aggregates.Sync;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>The change stamp store: the bump follows <see cref="ChangeStamp.Bump"/>, is atomic, and <c>AddUserChangeLog</c> rolls back.</summary>
[Collection("Database")]
public class UserChangeLogPersistenceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public UserChangeLogPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "changelog");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Bump_MatchesTheDomainRule_PerUser()
    {
        var store = new UserChangeLogStore(_db, TimeProvider.System);

        (await store.GetAsync("u1", CancellationToken.None)).Should().Be(ChangeStamp.Empty);
        await store.BumpAsync("u1", SyncArea.Habits, CancellationToken.None);
        var second = await store.BumpAsync("u1", SyncArea.Plan | SyncArea.Cardio, CancellationToken.None);
        await store.BumpAsync("u2", SyncArea.Mobility, CancellationToken.None);

        second.Should().Be(ChangeStamp.Empty.Bump(SyncArea.Habits).Bump(SyncArea.Plan | SyncArea.Cardio));
        (await store.GetAsync("u1", CancellationToken.None)).Should().Be(second);
        (await store.GetAsync("u2", CancellationToken.None)).Should().Be(new ChangeStamp(1, 0, 0, 0, 0, 1));
    }

    [Fact]
    public async Task ConcurrentBumps_EachGetTheirOwnVersion()
    {
        var versions = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = MigratedDatabase.Reopen(_db);
            return (await new UserChangeLogStore(db, TimeProvider.System).BumpAsync("racer", SyncArea.Habits, CancellationToken.None)).Version;
        }));

        versions.Should().BeEquivalentTo(Enumerable.Range(1, 10).Select(v => (long)v));
    }

    [Fact]
    public async Task Migration_DownDropsTheTable()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddUserChangeLog", StringComparison.Ordinal));

        await migrator.MigrateAsync(migrations[migrations.IndexOf(ours) - 1]);
        (await TableCountAsync()).Should().Be(0L);

        await migrator.MigrateAsync(ours);
        (await TableCountAsync()).Should().Be(1L);
    }

    private async Task<object?> TableCountAsync()
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_name = 'UserChangeLogs'";
        return await command.ExecuteScalarAsync();
    }
}
