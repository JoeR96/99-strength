using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>Finisher schedules on the real schema: jsonb nudges round-trip, the due query, and <c>AddFinisherSchedules</c> rolls back.</summary>
[Collection("Database")]
public class FinisherSchedulePersistenceTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Start = new(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public FinisherSchedulePersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "finisher");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Schedule_RoundTrips_AndIsFoundWhenDue()
    {
        var schedule = FinisherSchedule.Create(Joe, "gym-1", 3, null, [new FinisherNudge(Start.AddSeconds(180), "Hard: level 6 for 1 min")], Start);
        await new FinisherScheduleRepository(_db).AddAsync(schedule);
        await _db.SaveChangesAsync();

        await using var db = MigratedDatabase.Reopen(_db);
        var repo = new FinisherScheduleRepository(db);
        (await repo.ListDueAsync(Start.AddSeconds(179))).Should().BeEmpty();
        var due = (await repo.ListDueAsync(Start.AddSeconds(180))).Should().ContainSingle().Subject;
        due.Nudges.Should().ContainSingle().Which.Body.Should().Be("Hard: level 6 for 1 min");

        due.TakeDue(Start.AddSeconds(181));
        repo.Update(due);
        await db.SaveChangesAsync();

        await using var again = MigratedDatabase.Reopen(_db);
        var reloaded = await new FinisherScheduleRepository(again).GetAsync(Joe, "gym-1");
        reloaded!.Nudges.Single().SentAtUtc.Should().Be(Start.AddSeconds(181));
        reloaded.NextDueAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Migration_DownDropsTheTable()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddFinisherSchedules", StringComparison.Ordinal));

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
        command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_name = 'FinisherSchedules'";
        return await command.ExecuteScalarAsync();
    }
}
