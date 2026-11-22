using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Routine kinds, follow-along links, drill levels and the progression audit survive the database, and the
/// <c>AddMobilityLevels</c> migration backfills kinds and rolls back cleanly.
/// </summary>
[Collection("Database")]
public class MobilityLevelsPersistenceTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_mobility_levels_repo_01");
    private static readonly DateTimeOffset At = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public MobilityLevelsPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "mobilitylevels");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static MobilityDrill Ladder() => MobilityDrill.WithLevels("Reverse plank ladder",
    [
        new DrillLevel("Level 1", "Reverse tabletop, knees bent", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), "Hips high"),
        new DrillLevel("Level 2", "Hands on a bench, straight legs", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), null)
    ], 0, SideBias.Left, perSide: false);

    [Fact]
    public async Task RoundTrip_KeepsKindLevelsProgressAndAudit()
    {
        var routine = MobilityRoutine.Create(Owner, "Home yoga", 20, [Ladder()], MobilityRoutineKind.YogaFlow);
        routine.RecordFeedback("s1", [new DrillFeedback("Reverse plank ladder", DrillOutcome.Clean)], At);
        routine.RecordFeedback("s2", [new DrillFeedback("Reverse plank ladder", DrillOutcome.Clean)], At.AddDays(2));
        await new MobilityRoutineRepository(_db).AddAsync(routine);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new MobilityRoutineRepository(fresh).GetByIdAsync(routine.Id))!;

        loaded.Kind.Should().Be(MobilityRoutineKind.YogaFlow);
        loaded.Drills.Should().Equal(routine.Drills);
        loaded.Drills[0].Levels.Should().HaveCount(2);
        loaded.Drills[0].Dose.Amount.Should().Be(20);
        loaded.ProgressionLog.Should().Equal(routine.ProgressionLog);
    }

    [Fact]
    public async Task Undo_IsPersisted()
    {
        var routine = MobilityRoutine.Create(Owner, "Home yoga", 20, [Ladder()], MobilityRoutineKind.YogaFlow);
        routine.ApplyHoldIncrease(15, At);
        await new MobilityRoutineRepository(_db).AddAsync(routine);
        await _db.SaveChangesAsync();

        await using (var second = MigratedDatabase.Reopen(_db))
        {
            var repo = new MobilityRoutineRepository(second);
            var loaded = (await repo.GetByIdAsync(routine.Id))!;
            loaded.UndoLastChange(At);
            repo.Update(loaded);
            await second.SaveChangesAsync();
        }

        await using var third = MigratedDatabase.Reopen(_db);
        var reloaded = (await new MobilityRoutineRepository(third).GetByIdAsync(routine.Id))!;
        reloaded.Drills[0].Dose.Amount.Should().Be(15);
        reloaded.ProgressionLog.Should().OnlyContain(e => e.Undone);
    }

    [Fact]
    public async Task FollowAlong_RoundTripsWithItsLinkAndNoDrills()
    {
        var routine = MobilityRoutine.Create(Owner, "Hip video", 25, [], MobilityRoutineKind.FollowAlong, "https://example.com/flow");
        await new MobilityRoutineRepository(_db).AddAsync(routine);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new MobilityRoutineRepository(fresh).GetByIdAsync(routine.Id))!;

        loaded.Kind.Should().Be(MobilityRoutineKind.FollowAlong);
        loaded.FollowAlongUrl.Should().Be("https://example.com/flow");
        loaded.Drills.Should().BeEmpty();
    }

    [Fact]
    public async Task WarmUpKind_IsStoredEvenThoughItIsTheEnumDefault()
    {
        var routine = MobilityRoutine.Create(Owner, "Gym prep", 10,
            [new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, null, true)], MobilityRoutineKind.WarmUp);
        await new MobilityRoutineRepository(_db).AddAsync(routine);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        (await new MobilityRoutineRepository(fresh).GetByIdAsync(routine.Id))!.Kind.Should().Be(MobilityRoutineKind.WarmUp);
    }

    [Fact]
    public async Task Migration_BackfillsKindsByName_AndDownDropsTheColumns()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddMobilityLevels", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        (await ColumnExistsAsync("MobilityRoutines", "Kind")).Should().BeFalse();
        (await ColumnExistsAsync("MobilityRoutines", "ProgressionLog")).Should().BeFalse();
        (await ColumnExistsAsync("SessionCompletions", "DurationMinutes")).Should().BeFalse();

        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "MobilityRoutines" ("Id", "UserId", "Name", "TargetMinutes", "Drills") VALUES
            ('aaaaaaaa-0000-0000-0000-000000000001', 'u', 'Mobility A', 10, '[]'::jsonb),
            ('aaaaaaaa-0000-0000-0000-000000000002', 'u', 'Evening stretch', 15, '[]'::jsonb),
            ('aaaaaaaa-0000-0000-0000-000000000003', 'u', 'Evening yoga', 15, '[]'::jsonb);
            """);

        await migrator.MigrateAsync(ours);

        (await KindOfAsync("aaaaaaaa-0000-0000-0000-000000000001")).Should().Be("WarmUp");
        (await KindOfAsync("aaaaaaaa-0000-0000-0000-000000000002")).Should().Be("Stretch");
        (await KindOfAsync("aaaaaaaa-0000-0000-0000-000000000003")).Should().Be("YogaFlow");
        (await ColumnExistsAsync("SessionCompletions", "DurationMinutes")).Should().BeTrue();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private async Task<bool> ColumnExistsAsync(string table, string column) =>
        (long)(await ScalarAsync(
            $"SELECT count(*) FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{column}'"))! > 0;

    private async Task<string> KindOfAsync(string id) =>
        (string)(await ScalarAsync($"SELECT \"Kind\" FROM \"MobilityRoutines\" WHERE \"Id\" = '{id}'"))!;
}
