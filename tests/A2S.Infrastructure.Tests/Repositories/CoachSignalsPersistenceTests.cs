using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

[Collection("Database")]
public class CoachSignalsPersistenceTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_coach_repo_0001");
    private static readonly DateOnly Day = new(2026, 11, 22);
    private static readonly DateTimeOffset At = new(2026, 11, 22, 7, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public CoachSignalsPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "coach");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Readiness_RoundTrips_OnePerUserAndDate()
    {
        var entry = ReadinessEntry.Create(Owner, Day, ReadinessColour.Red, At);
        entry.SetKeepFinisher(true, At);
        await new ReadinessRepository(_db).AddAsync(entry);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new ReadinessRepository(fresh).GetForDateAsync(Owner, Day);

        loaded!.Colour.Should().Be(ReadinessColour.Red);
        loaded.KeepFinisher.Should().BeTrue();
        (await new ReadinessRepository(fresh).GetForDateAsync(Owner, Day.AddDays(1))).Should().BeNull();

        fresh.ReadinessEntries.Add(ReadinessEntry.Create(Owner, Day, ReadinessColour.Green, At));
        var duplicate = () => fresh.SaveChangesAsync();
        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CardioTrack_StartGate_RoundTrips()
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Day, withGate: true);
        await new CardioTrackRepository(_db).AddAsync(track);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new CardioTrackRepository(fresh).GetByIdAsync(track.Id))!;

        loaded.Status.Should().Be(CardioTrackStatus.Gated);
        loaded.StartGate.Should().BeEquivalentTo(track.StartGate);
    }

    [Fact]
    public async Task PlanPhase_ChangesAndSwaps_RoundTrip_AndOldPhasesLoadWithout()
    {
        var plan = TrainingPlan.Create(Owner, "Plan", Day, [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        plan.AddPhase(new PlanPhase(PhaseType.Cut, new DateOnly(2027, 1, 4), new DateOnly(2027, 2, 28), "Cut", "Build",
            ["Stair finisher: start intervals"], [new PhaseSwap("Pause Squat (Barbell)", "Squat (Barbell)")]));
        plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31), "Hold"));
        await new TrainingPlanRepository(_db).AddAsync(plan);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new TrainingPlanRepository(fresh).GetByIdAsync(plan.Id))!;

        var build = loaded.Phases[0];
        build.Label.Should().Be("Build");
        build.Changes.Should().Equal("Stair finisher: start intervals");
        build.Swaps.Should().Equal(new PhaseSwap("Pause Squat (Barbell)", "Squat (Barbell)"));
        loaded.Phases[1].Changes.Should().BeEmpty();
        loaded.Phases[1].Swaps.Should().BeEmpty();
    }

    [Fact]
    public async Task Migration_AddCoachSignals_DownThenUp_IsReversible()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddCoachSignals", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        (await TableExists("ReadinessEntries")).Should().BeFalse();
        (await ColumnExists("CardioTracks", "StartGate")).Should().BeFalse();
        (await TableExists("CardioTracks")).Should().BeTrue();

        await migrator.MigrateAsync();
        (await TableExists("ReadinessEntries")).Should().BeTrue();
        (await ColumnExists("CardioTracks", "StartGate")).Should().BeTrue();
    }

    private async Task<bool> TableExists(string table) =>
        await _db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_name = '{table}'").SingleAsync() > 0;

    private async Task<bool> ColumnExists(string table, string column) =>
        await _db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{column}'").SingleAsync() > 0;
}
