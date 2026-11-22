using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;
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
public class CardioTrackRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_track_repo_0001");
    private static readonly UserId OtherUser = new("user_track_repo_0002");
    private static readonly DateOnly Start = new(2026, 11, 2);
    private static readonly DateTimeOffset At = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public CardioTrackRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "tracks");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task RoundTrip_ReloadsStagesStateSessionsAuditBenchmarksAndSnapshot()
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);
        track.LogSession("s1", Start, new CardioLog(1800, 130, null, 3000m, null, effort: EffortRating.Easy), true, null, At);
        track.RecordBenchmark(Start.AddDays(28), 34.5m, "first");
        await new CardioTrackRepository(_db).AddAsync(track);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new CardioTrackRepository(fresh).GetByIdAsync(track.Id);

        loaded!.Name.Should().Be("Walk → run");
        loaded.Modality.Should().Be(CardioModality.Run);
        loaded.Stages.Should().Equal(track.Stages);
        loaded.State.Should().Be(track.State);
        loaded.Sessions.Should().Equal(track.Sessions);
        loaded.Audit.Should().Equal(track.Audit);
        loaded.Benchmarks.Should().Equal(track.Benchmarks);
        loaded.Benchmark.Should().Be(track.Benchmark);
        loaded.LastSnapshot!.State.Should().Be(track.LastSnapshot!.State);
        loaded.Prescription.Should().Be(track.Prescription);
    }

    [Fact]
    public async Task Update_PersistsEvaluationAndUndo()
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Owner, Start);
        await new CardioTrackRepository(_db).AddAsync(track);
        await _db.SaveChangesAsync();

        await using (var second = MigratedDatabase.Reopen(_db))
        {
            var repo = new CardioTrackRepository(second);
            var loaded = (await repo.GetByIdAsync(track.Id))!;
            loaded.LogSession("s1", Start, new CardioLog(300, null, null, null, null, 4, effort: EffortRating.Brutal), true, null, At);
            loaded.LogSession("s2", Start.AddDays(2), new CardioLog(300, null, null, null, null, 4, effort: EffortRating.Brutal), true, null, At);
            repo.Update(loaded);
            await second.SaveChangesAsync();
        }

        await using (var third = MigratedDatabase.Reopen(_db))
        {
            var repo = new CardioTrackRepository(third);
            var loaded = (await repo.GetByIdAsync(track.Id))!;
            loaded.Prescription.Title.Should().Be("6 min continuous @ level 6");
            loaded.UndoLastEvaluation();
            await third.SaveChangesAsync();
        }

        await using var fourth = MigratedDatabase.Reopen(_db);
        var final = (await new CardioTrackRepository(fourth).GetByIdAsync(track.Id))!;
        final.Prescription.Title.Should().Be("5 min continuous @ level 6");
        final.Sessions.Should().HaveCount(1);
        final.CanUndo.Should().BeFalse();
    }

    [Fact]
    public async Task ListForUser_IsScopedToTheOwner()
    {
        var repo = new CardioTrackRepository(_db);
        await repo.AddAsync(CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Owner, Start));
        await repo.AddAsync(CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, OtherUser, Start));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var tracks = await new CardioTrackRepository(fresh).ListForUserAsync(Owner);

        tracks.Should().ContainSingle().Which.Name.Should().Be("Walk builder");
    }

    [Fact]
    public async Task User_HeartRateAndZones_RoundTrip()
    {
        var user = User.Create("track-hr@example.com", "Track HR", Owner, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        user.SetHeartRate(188, [52, 62, 72, 82, 91]);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await fresh.Users.SingleAsync(u => u.Id == Owner);

        loaded.MaxHeartRate.Should().Be(188);
        loaded.PushRoutinesToHevy.Should().BeTrue("on by default");
        loaded.HeartRateZones!.LowerPercents.Should().Equal(52, 62, 72, 82, 91);
    }

    [Fact]
    public async Task SessionCompletion_OldCardioJsonWithoutNewFields_StillLoads_AndNewFieldsRoundTrip()
    {
        var trackId = new CardioTrackId(new Guid("bbbbbbbb-0000-0000-0000-000000000001"));
        var plan = TrainingPlan.Create(Owner, "Plan", Start,
            [ScheduledSession.Cardio(DayOfWeek.Monday, null, "Stairs", trackId),
             ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day1, "Upper A", finisher: SessionFinisher.ForTrack(trackId))],
            95m, new WeeklyAllowances(1, 1));
        plan.CompleteSession(Start, SessionKind.Cardio, CompletionSource.Manual, log: new CardioLog(600, 150, null, null, null, 6,
            effort: EffortRating.Brutal, intensityUnit: IntensityUnit.Level, intensityValue: 8, floors: 55, zone5Minutes: 2,
            pain: PainFlag.Niggle, painArea: "knee", source: CardioLogSource.Hevy), recordedAt: At, cardioTrack: trackId);
        plan.CompleteSession(Start.AddDays(1), SessionKind.Cardio, CompletionSource.Manual, log: new CardioLog(1200, 120, 5, null, "old"),
            recordedAt: At);
        await new TrainingPlanRepository(_db).AddAsync(plan);
        await _db.SaveChangesAsync();

        // Rewrite the second log to the pre-cardio-track JSON shape (no new keys at all).
        await _db.Database.ExecuteSqlRawAsync(
            """UPDATE "SessionCompletions" SET "Cardio" = '{{"DurationSeconds":1200,"AvgHeartRate":120,"Rpe":5,"Notes":"old"}}'::jsonb WHERE "Date" = '2026-11-03'""");

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new TrainingPlanRepository(fresh).GetByIdAsync(plan.Id))!;

        var tracked = loaded.Completions.Single(c => c.Date == Start);
        tracked.CardioTrackId.Should().Be(trackId);
        tracked.Cardio!.Floors.Should().Be(55);
        tracked.Cardio.Pain.Should().Be(PainFlag.Niggle);
        tracked.Cardio.Effort.Should().Be(EffortRating.Brutal);
        tracked.Cardio.Source.Should().Be(CardioLogSource.Hevy);
        loaded.Schedule.Single(s => s.Kind == SessionKind.Cardio).CardioTrackId.Should().Be(trackId);
        loaded.Schedule.Single(s => s.Kind == SessionKind.Lift).Finisher!.CardioTrackId.Should().Be(trackId);

        var old = loaded.Completions.Single(c => c.Date == Start.AddDays(1));
        old.Cardio!.Rpe.Should().Be(5);
        old.Cardio.Pain.Should().Be(PainFlag.None);
        old.Cardio.Source.Should().Be(CardioLogSource.Manual);
        old.CardioTrackId.Should().BeNull();
    }

    [Fact]
    public async Task Migration_DownThenUp_IsReversible()
    {
        var migrator = _db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261122152206_AddHevyWorkoutPosts");
        (await TableExists("CardioTracks")).Should().BeFalse();
        (await ColumnExists("SessionCompletions", "CardioTrackId")).Should().BeFalse();
        (await ColumnExists("Users", "MaxHeartRate")).Should().BeFalse();

        await migrator.MigrateAsync();
        (await TableExists("CardioTracks")).Should().BeTrue();
        (await ColumnExists("Users", "HeartRateZonePercents")).Should().BeTrue();
        (await ColumnExists("Users", "PushRoutinesToHevy")).Should().BeTrue();
        (await TableExists("HevyWorkoutPosts")).Should().BeTrue("the gym-mode migration below this one is untouched");
    }

    private async Task<bool> TableExists(string table) =>
        await _db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_name = '{table}'").SingleAsync() > 0;

    private async Task<bool> ColumnExists(string table, string column) =>
        await _db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{column}'").SingleAsync() > 0;
}
