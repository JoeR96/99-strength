using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;

namespace A2S.Domain.Tests.Aggregates.GymPlanFit;

public class PendingTrainingMaxTests
{
    private static Exercise PendingBench(ProgramTier tier = ProgramTier.Primary) =>
        Exercise.CreateWithPendingTrainingMax("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", tier: tier);

    [Fact]
    public void Pending_exercise_plans_the_a2s2_sets_and_reps_without_weight()
    {
        var pending = PendingBench();
        var known = new ExerciseBuilder().AsLinear(100m).Build();

        var pendingSets = pending.CalculatePlannedSets(1, 1).ToList();
        var knownSets = known.CalculatePlannedSets(1, 1).ToList();

        pending.IsTrainingMaxPending.Should().BeTrue();
        pending.GetTrainingMax().Should().BeNull();
        pendingSets.Select(s => (s.SetNumber, s.TargetReps, s.IsAmrap))
            .Should().Equal(knownSets.Select(s => (s.SetNumber, s.TargetReps, s.IsAmrap)));
        pendingSets.Should().OnlyContain(s => s.Weight.Value == 0m);
    }

    [Fact]
    public void Auxiliary_tier_follows_the_auxiliary_scheme()
    {
        var aux = PendingBench(ProgramTier.Auxiliary);

        aux.CalculatePlannedSets(1, 1).First().TargetReps
            .Should().Be(A2SHypertrophyProgram.GetWeekData(1, ProgramTier.Auxiliary).RepsPerSet);
    }

    [Fact]
    public void Completing_a_pending_exercise_is_refused_with_a_prompt()
    {
        var exercise = PendingBench();
        var workout = new WorkoutBuilder().WithVariant(ProgramVariant.FourDay).WithExercise(exercise).Build();
        workout.Start();
        var planned = exercise.CalculatePlannedSets(1, 1).ToList();
        var performance = new ExercisePerformance(exercise.Id, planned,
            [new CompletedSet(1, Weight.Create(60, WeightUnit.Kilograms), 5)], new DateTime(2026, 11, 23));

        var act = () => workout.CompleteDay(DayNumber.Day1, [performance]);

        act.Should().Throw<DomainException>().WithMessage("*Set your training max for Bench Press (Barbell)*");
    }

    [Fact]
    public void Setting_the_training_max_clears_pending_and_plans_weights()
    {
        var exercise = PendingBench();
        var workout = new WorkoutBuilder().WithVariant(ProgramVariant.FourDay).WithExercise(exercise).Build();

        workout.AdjustTrainingMax(exercise.Id, TrainingMax.Create(100m, WeightUnit.Kilograms), "Entered");

        exercise.IsTrainingMaxPending.Should().BeFalse();
        exercise.CalculatePlannedSets(1, 1).Should().OnlyContain(s => s.Weight.Value == TrainingMax.Create(100m, WeightUnit.Kilograms).CalculateWorkingWeight(A2SHypertrophyProgram.GetWeekData(1, ProgramTier.Primary).Intensity).Value);
    }

    [Theory]
    [InlineData(100, 5, 105)]   // e1RM 116.67 × 0.9 = 105
    [InlineData(80, 8, 90)]     // e1RM 101.33 × 0.9 = 91.2 → 90
    [InlineData(100, 1, 90)]
    public void Estimates_tm_from_a_recent_set_with_epley_and_ninety_percent(decimal weight, int reps, decimal expected)
    {
        TrainingMaxEstimator.FromRecentSet(Weight.Create(weight, WeightUnit.Kilograms), reps).Value.Should().Be(expected);
    }

    [Fact]
    public void Estimate_rejects_sets_too_long_to_estimate_from()
    {
        var act = () => TrainingMaxEstimator.FromRecentSet(Weight.Create(60, WeightUnit.Kilograms), 20);
        act.Should().Throw<BusinessRuleViolationException>();
    }
}

public class SupersetAndExtraSetTests
{
    [Fact]
    public void Exercises_pair_into_a_superset_group_of_two()
    {
        var a = new ExerciseBuilder().WithName("Row").WithOrder(2).AsRepsPerSet().Build();
        var b = new ExerciseBuilder().WithName("Raise").WithOrder(3).AsRepsPerSet().Build();
        var c = new ExerciseBuilder().WithName("Curl").WithOrder(4).AsRepsPerSet().Build();
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().WithExercise(a).WithExercise(b).WithExercise(c).Build();

        workout.SetExerciseSupersetGroup(a.Id, 2);
        workout.SetExerciseSupersetGroup(b.Id, 2);
        var third = () => workout.SetExerciseSupersetGroup(c.Id, 2);

        a.SupersetGroup.Should().Be(2);
        b.SupersetGroup.Should().Be(2);
        third.Should().Throw<DomainException>().WithMessage("*pairs two*");
        workout.SetExerciseSupersetGroup(a.Id, null);
        a.SupersetGroup.Should().BeNull();
    }

    [Fact]
    public void Extra_set_side_needs_a_unilateral_reps_per_set_exercise()
    {
        var split = new ExerciseBuilder().WithName("Split Squat").WithOrder(2).AsRepsPerSet(isUnilateral: true, startingSets: 2, targetSets: 3).Build();
        var bilateral = new ExerciseBuilder().WithName("Leg Press").WithOrder(3).AsRepsPerSet().Build();
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().WithExercise(split).WithExercise(bilateral).Build();

        workout.SetExerciseExtraSetSide(split.Id, ExtraSetSide.Left);
        var act = () => workout.SetExerciseExtraSetSide(bilateral.Id, ExtraSetSide.Left);

        split.ExtraSetSide.Should().Be(ExtraSetSide.Left);
        act.Should().Throw<DomainException>();
    }
}

public class FixedFinisherTests
{
    [Fact]
    public void A_finisher_can_be_a_fixed_ten_minute_treadmill_cool_down()
    {
        var finisher = SessionFinisher.ForFixed(FixedActivity.EasyInclineTreadmill());

        finisher.Fixed!.Minutes.Should().Be(10);
        finisher.Fixed.Kind.Should().Be(FixedActivityKind.Treadmill);
        finisher.Fixed.Describe().Should().Be("10 min · Easy incline");
        finisher.CardioProtocolId.Should().BeNull();
        finisher.CardioTrackId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Fixed_cool_down_minutes_are_bounded(int minutes)
    {
        var act = () => new FixedActivity(FixedActivityKind.Treadmill, minutes);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void A_finisher_cannot_be_both_a_track_and_a_fixed_cool_down()
    {
        var act = () => new SessionFinisher(cardioTrackId: new CardioTrackId(new Guid("11111111-1111-1111-1111-111111111111")), @fixed: FixedActivity.EasyInclineTreadmill());
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void A_lift_slot_carries_a_fixed_cool_down()
    {
        var slot = ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A", finisher: SessionFinisher.ForFixed(FixedActivity.EasyInclineTreadmill()));
        slot.Finisher!.Fixed.Should().NotBeNull();
    }
}
