using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace A2S.Application.Tests.Hevy;

/// <summary>
/// Matching order and review focus 2: BST ends on 25 Oct 2026, so a workout started at 23:30Z on
/// Friday 23 Oct is 00:30 local on Saturday 24 Oct and belongs to Saturday's session, not Friday's.
/// </summary>
public class WorkoutMatcherTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-000000000001"));
    private static readonly LocalClock Clock = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));

    private static readonly Dictionary<string, string> TemplateLinks = new()
    {
        ["T-breathing"] = "drill:90/90-breathing",
        ["T-open-books"] = "drill:open-books",
        ["T-4x4"] = "cardio:NorwegianFourByFour",
        ["T-seal-row"] = "exercise:seal-row-barbell"
    };

    private readonly TrainingPlan _plan;
    private readonly Workout _program;

    public WorkoutMatcherTests()
    {
        // Plan starts Monday 19 Oct 2026 (BST ends 25 Oct 2026 at 01:00 UTC).
        _plan = TrainingPlan.Create(Joe, "Cut to 85", new DateOnly(2026, 10, 19),
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
            ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
            ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B"),
            ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K run or LISS"),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);

        var deadlift = Exercise.CreateWithLinearProgression("Deadlift (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day4, 1, "C6272009", TrainingMax.Create(160m, WeightUnit.Kilograms));
        var legPress = Exercise.CreateWithRepsPerSetProgression("Leg Press (Machine)", ExerciseCategory.Accessory, EquipmentType.Machine,
            DayNumber.Day4, 2, "C7973E0E", RepRange.Create(10, 10), 3, 5);
        var sealRow = Exercise.CreateWithRepsPerSetProgression("Seal Row (Barbell)", ExerciseCategory.Accessory, EquipmentType.Barbell,
            DayNumber.Day4, 3, "custom:seal-row-barbell", RepRange.Create(10, 10), 3, 5);
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        _program = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay)
            .WithExercise(bench).WithExercise(deadlift).WithExercise(legPress).WithExercise(sealRow).Build();
        _plan.LinkWorkout(_program.Id);
    }

    private static HevyWorkout Workout(string title, DateTimeOffset start, string? routineId = null, params string[] templateIds) => new()
    {
        Id = "w-1",
        Title = title,
        RoutineId = routineId,
        StartTime = start,
        EndTime = start.AddMinutes(45),
        Exercises = templateIds.Select((id, i) => new HevyWorkoutExercise { Index = i, ExerciseTemplateId = id, Sets = [new HevyWorkoutSet { Reps = 5 }] }).ToList()
    };

    private MatchResult Match(HevyWorkout workout, params HevyLink[] routineLinks) =>
        WorkoutMatcher.Match(workout, _plan, routineLinks, _program, TemplateLinks, Clock);

    [Fact]
    public void ReviewFocus2_LateFridayNightInBst_IsSaturdayLocally_AndMatchesTheCardioSlotNotTheLift()
    {
        // 23:30Z on Friday 23 Oct 2026 is 00:30 BST on Saturday 24 Oct.
        var run = Workout("Evening run", new DateTimeOffset(2026, 10, 23, 23, 30, 0, TimeSpan.Zero), null, "T-4x4");

        var result = Match(run);

        var matched = result.Should().BeOfType<MatchResult.Matched>().Subject;
        matched.Date.Should().Be(new DateOnly(2026, 10, 24));
        matched.Session.Kind.Should().Be(SessionKind.Cardio);
        matched.Session.Label.Should().Be("C25K run or LISS");
    }

    [Fact]
    public void AfterTheClocksGoBack_2330ZIsStillTheSameDay()
    {
        // 23:30Z on Friday 30 Oct 2026 is 23:30 GMT: still Friday, so the lift's exercises match Lower B.
        var lift = Workout("Workout", new DateTimeOffset(2026, 10, 30, 23, 30, 0, TimeSpan.Zero), null, "C6272009", "C7973E0E");

        var result = Match(lift);

        var matched = result.Should().BeOfType<MatchResult.Matched>().Subject;
        matched.Date.Should().Be(new DateOnly(2026, 10, 30));
        matched.Session.LiftDay.Should().Be(DayNumber.Day4);
    }

    [Fact]
    public void RoutineLink_WinsOverEverythingElse_EvenOnAnotherDay()
    {
        var key = HevyLocalKeys.LiftRoutine(_plan.Id, new DateOnly(2026, 10, 19), DayNumber.Day1);
        var link = HevyLink.Create(Joe, HevyLinkKind.Routine, key, "routine-upper-a", DateTimeOffset.UtcNow);
        // Done on Tuesday (Lower A's day) from the Upper A routine, titled unhelpfully.
        var workout = Workout("Lower A", new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), "routine-upper-a", "79D0BB3A");

        var result = Match(workout, link);

        var matched = result.Should().BeOfType<MatchResult.Matched>().Subject;
        matched.Session.LiftDay.Should().Be(DayNumber.Day1);
        matched.Date.Should().Be(new DateOnly(2026, 10, 20));
        matched.How.Should().Be("routine");
    }

    [Fact]
    public void RoutineLinkForAnotherPlan_IsIgnored_AndTitleMatchingTakesOver()
    {
        var otherPlan = new TrainingPlanId(Guid.NewGuid());
        var link = HevyLink.Create(Joe, HevyLinkKind.Routine, HevyLocalKeys.LiftRoutine(otherPlan, new DateOnly(2026, 10, 19), DayNumber.Day1), "routine-x", DateTimeOffset.UtcNow);
        var workout = Workout("Cut to 85 · W1 Lower A", new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), "routine-x", "D04AC939");

        var result = Match(workout, link);

        result.Should().BeOfType<MatchResult.Matched>().Which.Session.Label.Should().Be("Lower A");
    }

    [Fact]
    public void TitleContainingTheLabel_MatchesCaseInsensitively()
    {
        var workout = Workout("W2 upper a — felt strong", new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero), null, "79D0BB3A");

        var result = Match(workout);

        var matched = result.Should().BeOfType<MatchResult.Matched>().Subject;
        matched.Session.Label.Should().Be("Upper A");
        matched.How.Should().Be("title");
    }

    [Fact]
    public void MobilityOnlyWorkout_TakesTheMobilitySession_OnATwoSessionDay()
    {
        var workout = Workout("Stretch", new DateTimeOffset(2026, 10, 21, 7, 0, 0, TimeSpan.Zero), null, "T-breathing", "T-open-books");

        var result = Match(workout);

        var matched = result.Should().BeOfType<MatchResult.Matched>().Subject;
        matched.Session.Kind.Should().Be(SessionKind.Mobility);
        matched.Session.MobilityRoutineId.Should().Be(MobilityB);
    }

    [Fact]
    public void MobilityOnlyWorkout_OnALiftDay_IsUnmatched_NotALiftCompletion()
    {
        var workout = Workout("Warm-up only", new DateTimeOffset(2026, 10, 19, 17, 0, 0, TimeSpan.Zero), null, "T-breathing");

        var result = Match(workout);

        result.Should().BeOfType<MatchResult.Unmatched>().Which.Reason.Should().Contain("mobility-only");
    }

    [Fact]
    public void ExerciseOverlap_NeedsSixtyPercent_CountingCustomTemplatesThroughTheirLinks()
    {
        var friday = new DateTimeOffset(2026, 10, 23, 18, 0, 0, TimeSpan.Zero);

        var twoOfThree = Match(Workout("Gym", friday, null, "C6272009", "T-seal-row", "ZZZ"));
        var oneOfThree = Match(Workout("Gym", friday, null, "C6272009", "ZZZ"));

        twoOfThree.Should().BeOfType<MatchResult.Matched>().Which.How.Should().Be("exercise overlap");
        oneOfThree.Should().BeOfType<MatchResult.Unmatched>().Which.Reason.Should().Be("only 1 of 3 planned exercises present");
    }

    [Fact]
    public void RestDay_IsUnmatchedAsRestDay()
    {
        var workout = Workout("Bonus session", new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero), null, "79D0BB3A");

        var result = Match(workout);

        var unmatched = result.Should().BeOfType<MatchResult.Unmatched>().Subject;
        unmatched.Reason.Should().Be("rest day");
        unmatched.Date.Should().Be(new DateOnly(2026, 10, 25));
    }

    [Fact]
    public void BeforeThePlanStarted_IsUnmatched()
    {
        var workout = Workout("Upper A", new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.Zero), null, "79D0BB3A");

        Match(workout).Should().BeOfType<MatchResult.Unmatched>().Which.Reason.Should().Be("before the plan started");
    }

    [Fact]
    public void ThursdayHasNothingScheduled()
    {
        var workout = Workout("Random", new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero), null, "79D0BB3A");

        Match(workout).Should().BeOfType<MatchResult.Unmatched>().Which.Reason.Should().Be("nothing scheduled");
    }
}
