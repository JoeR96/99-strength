using System.Text.Json;
using A2S.Application.Cardio;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.GymSessions;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using A2S.Infrastructure.SeedData;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// <see cref="CardioExerciseBackfill"/> on the production shape of 24 Nov 2026: Falador Fat Furnace started Monday
/// 23 Nov, Upper A done in gym mode with a 5-minute Easy stair finisher (33 floors, 660 s) posted to Hevy.
/// </summary>
[Collection("Database")]
public class CardioExerciseBackfillTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_cardio_backfill_01");
    private static readonly DateOnly Mon23Nov = new(2026, 11, 23);
    private static readonly DateOnly Tue24Nov = new(2026, 11, 24);
    private static readonly DateTimeOffset At = new(2026, 11, 23, 19, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ClientSession = Guid.Parse("4cf8f75a-0000-4000-8000-000000000001");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string StairBlock = "3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)";
    private static readonly Weight Kg60 = Weight.Kilograms(60m);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public CardioExerciseBackfillTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "cardiobackfill");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private sealed record Seeded(TrainingPlanId PlanId, WorkoutId WorkoutId, CardioTrackId StairTrackId, HevyWorkoutPostId PostId);

    private static Exercise Lift(string name, DayNumber day) =>
        new ExerciseBuilder().WithName(name).WithDay(day).WithOrder(1).WithTemplateId($"tpl-{name}").AsRepsPerSet(startingWeight: Kg60).Build();

    private static ExercisePerformance Done(Exercise lift) =>
        new(lift.Id, [new PlannedSet(1, Kg60, 10)], [new CompletedSet(1, Kg60, 10)], At.UtcDateTime);

    private static CardioLog StairLog() =>
        new CardioLog(660, null, 4, null, null, effort: EffortRating.Easy, intensityUnit: IntensityUnit.Level, intensityValue: 6, floors: 33)
        {
            WorkMinutes = 5m
        };

    /// <summary>The production data: plan, workout at W1 Day 2, stair track with one session, the Posted Hevy post.</summary>
    private async Task<Seeded> ProductionShapeAsync(string planName = "Falador Fat Furnace", UserId? user = null)
    {
        var owner = user ?? Owner;
        var lifts = new[]
        {
            Lift("Bench Press (Barbell)", DayNumber.Day1), Lift("Pause Squat (Barbell)", DayNumber.Day2),
            Lift("Incline Bench Press (Smith Machine)", DayNumber.Day3), Lift("Front Squat", DayNumber.Day4)
        };
        var workout = Workout.Create(owner, planName, ProgramVariant.FourDay, lifts);
        workout.Start();
        workout.CompleteDay(DayNumber.Day1, [Done(lifts[0])]);

        var stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, owner, Mon23Nov);
        var coolDown = SessionFinisher.ForFixed(FixedActivity.EasyInclineTreadmill(10));
        var plan = TrainingPlan.Create(owner, planName, Mon23Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", finisher: SessionFinisher.ForTrack(stairs.Id)),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A", finisher: coolDown),
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B", finisher: SessionFinisher.ForTrack(stairs.Id)),
            ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B", finisher: coolDown),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);
        plan.LinkWorkout(workout.Id);
        var lift = plan.CompleteSession(Mon23Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day1, recordedAt: At);
        var finisher = plan.CompleteSession(Mon23Nov, SessionKind.Cardio, CompletionSource.Manual, log: StairLog(), recordedAt: At,
            cardioTrack: stairs.Id);
        stairs.LogSession(finisher.TrackSessionKey, Mon23Nov, StairLog(), true, null, At);
        plan.LinkCompletionToHevy(lift.Id, "b632f498");

        var payload = new GymSessionPayload
        {
            WorkoutId = workout.Id.Value,
            Day = DayNumber.Day1,
            WarmUpTicks = [true, true],
            Performances =
            [
                new ExercisePerformanceRequest
                {
                    ExerciseId = lifts[0].Id.Value,
                    CompletedSets = [new CompletedSetRequest { SetNumber = 1, Weight = 60m, WeightUnit = WeightUnit.Kilograms, ActualReps = 10 }]
                }
            ],
            FinisherTrackId = stairs.Id.Value,
            FinisherPrescription = "3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)",
            Finisher = new FinisherLogRequest(660, Effort.Easy, Level: 6, Floors: 33, WorkMinutes: 5m)
        };
        var post = HevyWorkoutPost.Create(owner, ClientSession, plan.Id, lift.Id, Mon23Nov, $"{planName} · W1 Upper A",
            At.AddMinutes(-65), At, JsonSerializer.Serialize(payload, Json), "{}", At);
        post.MarkPosted("b632f498", At);

        await new WorkoutRepository(_db).AddAsync(workout);
        await new TrainingPlanRepository(_db).AddAsync(plan);
        await new CardioTrackRepository(_db).AddAsync(stairs);
        await new HevyWorkoutPostRepository(_db).AddAsync(post);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return new Seeded(plan.Id, workout.Id, stairs.Id, post.Id);
    }

    private Task<int> BackfillAsync() => CardioExerciseBackfill.RunAsync(_db, NullLogger.Instance, Now);

    private async Task<Workout> WorkoutAsync(WorkoutId id)
    {
        await using var fresh = MigratedDatabase.Reopen(_db);
        return (await new WorkoutRepository(fresh).GetByIdAsync(id))!;
    }

    private static Exercise CardioOn(Workout workout, DayNumber day) =>
        workout.Exercises.Single(e => e.AssignedDay == day && e.Category == ExerciseCategory.Cardio);

    private static string Block(Workout workout, DayNumber day) => CardioOn(workout, day).Progression.CalculateCardioBlock()!.Text;

    [Fact]
    public async Task Backfill_OneEasyFiveMinuteSession_SeedsSixMinutesLevelSix()
    {
        var seeded = await ProductionShapeAsync();

        (await BackfillAsync()).Should().Be(1);

        var workout = await WorkoutAsync(seeded.WorkoutId);
        workout.Exercises.Where(e => e.Category == ExerciseCategory.Cardio)
            .Select(e => (e.AssignedDay, e.OrderInDay, e.Name, e.ExternalTemplateId)).Should().BeEquivalentTo(new[]
            {
                (DayNumber.Day1, 2, "Stair Machine (Floors)", "4377A52B"), (DayNumber.Day2, 2, "Treadmill", "243710DE"),
                (DayNumber.Day3, 2, "Stair Machine (Floors)", "4377A52B"), (DayNumber.Day4, 2, "Treadmill", "243710DE")
            });
        Block(workout, DayNumber.Day1).Should().Be(StairBlock);
        Block(workout, DayNumber.Day3).Should().Be(StairBlock);
        CardioOn(workout, DayNumber.Day2).IsCardioSettingsPending.Should().BeTrue();
        CardioOn(workout, DayNumber.Day4).IsCardioSettingsPending.Should().BeTrue();
        var seed = workout.AuditEntries.Should().ContainSingle(a => a.Type == AuditEntryType.ManualAdjustment).Subject;
        seed.Reason.Should().Be("Seeded from the stair finisher track");
        seed.NewValue.Should().Be(StairBlock);
        (workout.CurrentWeek, workout.CurrentDay).Should().Be((1, 2));
    }

    [Fact]
    public async Task Backfill_RetroLogsMondaysStairsIntoDayOneHistory()
    {
        var seeded = await ProductionShapeAsync();

        await BackfillAsync();

        var workout = await WorkoutAsync(seeded.WorkoutId);
        var monday = workout.CompletedActivities.Should().ContainSingle().Subject;
        var stairs = monday.Performances.Should().HaveCount(2).And.ContainSingle(p => p.Cardio != null).Subject;
        stairs.ExerciseId.Should().Be(CardioOn(workout, DayNumber.Day1).Id);
        stairs.PlannedCardio!.Text.Should().Be(StairBlock);
        (stairs.Cardio!.Finished, stairs.Cardio.Effort, stairs.Cardio.Pain, stairs.Cardio.TotalSeconds, stairs.Cardio.WorkMinutes,
                stairs.Cardio.Intensity, stairs.Cardio.Floors)
            .Should().Be((true, EffortRating.Easy, PainFlag.None, 660, (decimal?)5m, (decimal?)6m, (int?)33));
        CardioSummaryFormatter.For(stairs).Should().Be("5 min @ level 6 · Easy");
    }

    [Fact]
    public async Task Backfill_RetroLogsAFixedCoolDown_AsWalkHistoryOnly()
    {
        var seeded = await ProductionShapeAsync();
        var workout = (await new WorkoutRepository(_db).GetByIdAsync(seeded.WorkoutId))!;
        workout.CompleteDay(DayNumber.Day2, [Done(workout.Exercises.Single(e => e.AssignedDay == DayNumber.Day2))]);
        var plan = (await new TrainingPlanRepository(_db).GetByIdAsync(seeded.PlanId))!;
        plan.CompleteSession(Tue24Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day2, recordedAt: Now);
        plan.CompleteSession(Tue24Nov, SessionKind.Cardio, CompletionSource.Manual, log: new CardioLog(600, null, 4, null, null), recordedAt: Now);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await BackfillAsync();

        var after = await WorkoutAsync(seeded.WorkoutId);
        var tuesday = after.CompletedActivities.Single(a => a.Day == DayNumber.Day2);
        var walk = tuesday.Performances.Should().ContainSingle(p => p.Cardio != null).Subject;
        walk.ExerciseId.Should().Be(CardioOn(after, DayNumber.Day2).Id);
        (walk.Cardio!.Finished, walk.Cardio.Effort, walk.Cardio.TotalSeconds).Should().Be((true, EffortRating.Easy, 600));
        CardioOn(after, DayNumber.Day2).IsCardioSettingsPending.Should().BeTrue("the walk is history only: speed and incline are still asked");
        Block(after, DayNumber.Day4).Should().Be("10 min @ ? % incline");
    }

    [Fact]
    public async Task Backfill_MarksThatDaysHevyPostForUpdate_WithNewPayload()
    {
        var seeded = await ProductionShapeAsync();

        await BackfillAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var post = await fresh.HevyWorkoutPosts.SingleAsync(p => p.Id == seeded.PostId);
        post.Status.Should().Be(HevyPostStatus.UpdatePending);
        post.HevyWorkoutId.Should().Be("b632f498");
        post.IsDue(Now).Should().BeTrue();
        var payload = JsonSerializer.Deserialize<GymSessionPayload>(post.PayloadJson, Json)!;
        (payload.Finisher, payload.FinisherTrackId, payload.FinisherPrescription, payload.FinisherFixedKind).Should().Be((null, null, null, null));
        payload.Week.Should().Be(1);
        payload.Day.Should().Be(DayNumber.Day1);
        payload.WarmUpTicks.Should().Equal(true, true);
        payload.Performances.Should().HaveCount(2);
        var workout = await WorkoutAsync(seeded.WorkoutId);
        var stairs = payload.Performances.Single(p => p.Cardio != null);
        stairs.ExerciseId.Should().Be(CardioOn(workout, DayNumber.Day1).Id.Value);
        stairs.Cardio.Should().Be(new CardioResultRequest(true, Effort.Easy, PainFlag.None, null, 660, 5m, 6m, null, 33));
    }

    [Fact]
    public async Task Backfill_ClearsFinishers_AndPausesStairTrack()
    {
        var seeded = await ProductionShapeAsync();

        await BackfillAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var plan = (await new TrainingPlanRepository(fresh).GetByIdAsync(seeded.PlanId))!;
        plan.Schedule.Where(s => s.Kind == SessionKind.Lift).Should().HaveCount(4).And.OnlyContain(s => s.Finisher == null);
        plan.Schedule.Select(s => s.Label).Should().Equal("Upper A", "Lower A", "Upper B", "Lower B", "Rest");
        var track = (await new CardioTrackRepository(fresh).GetByIdAsync(seeded.StairTrackId))!;
        track.Status.Should().Be(CardioTrackStatus.Paused);
        track.Sessions.Should().ContainSingle("history is kept");
    }

    [Fact]
    public async Task Backfill_IsIdempotent()
    {
        var seeded = await ProductionShapeAsync();
        (await BackfillAsync()).Should().Be(1);
        _db.ChangeTracker.Clear();
        var workout = (await new WorkoutRepository(_db).GetByIdAsync(seeded.WorkoutId))!;
        workout.SetCardioSettings(CardioOn(workout, DayNumber.Day2).Id, 5m, 5.5m);
        workout.CompleteDay(DayNumber.Day2, [Done(workout.Exercises.Single(e => e.AssignedDay == DayNumber.Day2 && e.Category != ExerciseCategory.Cardio))]);
        var post = await _db.HevyWorkoutPosts.SingleAsync(p => p.Id == seeded.PostId);
        post.MarkUpdated();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var before = await WorkoutAsync(seeded.WorkoutId);

        (await BackfillAsync()).Should().Be(0);

        var after = await WorkoutAsync(seeded.WorkoutId);
        after.Exercises.Should().HaveCount(before.Exercises.Count);
        after.Exercises.Count(e => e.Category == ExerciseCategory.Cardio).Should().Be(4);
        after.CompletedActivities.Sum(a => a.Performances.Count).Should().Be(before.CompletedActivities.Sum(a => a.Performances.Count));
        after.AuditEntries.Should().HaveCount(before.AuditEntries.Count);
        Block(after, DayNumber.Day4).Should().Be("10 min @ 5 % incline, 5.5 km/h", "progress made since is kept");
        Block(after, DayNumber.Day1).Should().Be(StairBlock);
        await using var fresh = MigratedDatabase.Reopen(_db);
        (await fresh.HevyWorkoutPosts.SingleAsync(p => p.Id == seeded.PostId)).Status.Should().Be(HevyPostStatus.Posted);
    }

    [Fact]
    public async Task Backfill_IgnoresOtherPlans()
    {
        var other = await ProductionShapeAsync("Lumbridge Lard Shed");
        var formerName = await ProductionShapeAsync("Plan v3 — Base/Build/Transition", new UserId("user_cardio_backfill_02"));

        (await BackfillAsync()).Should().Be(1, "a plan started under plan v3's former name is still plan v3");

        var untouched = await WorkoutAsync(other.WorkoutId);
        untouched.Exercises.Should().NotContain(e => e.Category == ExerciseCategory.Cardio);
        untouched.CompletedActivities.Single().Performances.Should().ContainSingle();
        await using var fresh = MigratedDatabase.Reopen(_db);
        (await fresh.HevyWorkoutPosts.SingleAsync(p => p.Id == other.PostId)).Status.Should().Be(HevyPostStatus.Posted);
        (await new CardioTrackRepository(fresh).GetByIdAsync(other.StairTrackId))!.Status.Should().Be(CardioTrackStatus.Active);
        (await new TrainingPlanRepository(fresh).GetByIdAsync(other.PlanId))!.Schedule[0].Finisher.Should().NotBeNull();
        (await WorkoutAsync(formerName.WorkoutId)).Exercises.Should().Contain(e => e.Category == ExerciseCategory.Cardio);
    }
}
