using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Phase 3 tests: verify polymorphic dispatch on ExerciseProgression abstractions,
/// OCP compliance, and new equipment type handling.
/// </summary>
public class StrategyPolymorphismTests
{
    private readonly ExerciseId _exerciseId = new(Guid.Parse("eee44444-4444-4444-4444-444444444444"));
    private readonly TrainingMax _tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
    private readonly Weight _weight = Weight.Create(20m, WeightUnit.Kilograms);
    private readonly RepRange _repRange = RepRange.Create(8, 15);


    [Fact]
    public void WhenLinearStrategy_GetCurrentWeightReturnsNull()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        strategy.GetCurrentWeight().Should().BeNull();
    }

    [Fact]
    public void WhenRepsPerSetStrategy_GetCurrentWeightReturnsWeight()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        strategy.GetCurrentWeight().Should().NotBeNull();
        strategy.GetCurrentWeight()!.Value.Should().Be(20m);
    }

    [Fact]
    public void WhenMinimalSetsStrategy_GetCurrentWeightReturnsWeight()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        strategy.GetCurrentWeight().Should().NotBeNull();
        strategy.GetCurrentWeight()!.Value.Should().Be(20m);
    }



    [Fact]
    public void WhenLinearStrategy_GetTrainingMaxReturnsTm()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        strategy.GetTrainingMax().Should().NotBeNull();
        strategy.GetTrainingMax()!.Value.Should().Be(100m);
    }

    [Fact]
    public void WhenRepsPerSetStrategy_GetTrainingMaxReturnsNull()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        strategy.GetTrainingMax().Should().BeNull();
    }

    [Fact]
    public void WhenMinimalSetsStrategy_GetTrainingMaxReturnsNull()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        strategy.GetTrainingMax().Should().BeNull();
    }



    [Fact]
    public void WhenLinearStrategy_SupportsUnilateralIsFalse()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        strategy.SupportsUnilateral.Should().BeFalse();
    }

    [Fact]
    public void WhenRepsPerSetStrategy_SupportsUnilateralIsTrue()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        strategy.SupportsUnilateral.Should().BeTrue();
    }

    [Fact]
    public void WhenMinimalSetsStrategy_SupportsUnilateralIsFalse()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        strategy.SupportsUnilateral.Should().BeFalse();
    }



    [Fact]
    public void WhenLinearStrategy_SetUnilateralThrows()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        var act = () => strategy.SetUnilateral(true);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WhenRepsPerSetStrategy_SetUnilateralWorks()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Dumbbell, startingWeight: _weight);
        strategy.IsUnilateral.Should().BeFalse();
        strategy.SetUnilateral(true);
        strategy.IsUnilateral.Should().BeTrue();
    }

    [Fact]
    public void WhenMinimalSetsStrategy_SetUnilateralThrows()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        var act = () => strategy.SetUnilateral(true);
        act.Should().Throw<InvalidOperationException>();
    }



    [Fact]
    public void WhenLinearStrategy_UpdateWeightThrows()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        var act = () => strategy.UpdateWeight(Weight.Create(50m, WeightUnit.Kilograms));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WhenRepsPerSetStrategy_UpdateWeightWorks()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        strategy.UpdateWeight(Weight.Create(25m, WeightUnit.Kilograms));
        strategy.GetCurrentWeight()!.Value.Should().Be(25m);
    }

    [Fact]
    public void WhenMinimalSetsStrategy_UpdateWeightWorks()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        strategy.UpdateWeight(Weight.Create(25m, WeightUnit.Kilograms));
        strategy.GetCurrentWeight()!.Value.Should().Be(25m);
    }



    [Fact]
    public void WhenLinearStrategy_UpdateTrainingMaxValueReturnsEvent()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        var newTm = TrainingMax.Create(110m, WeightUnit.Kilograms);
        var evt = strategy.UpdateTrainingMaxValue(newTm, "test");
        evt.Should().NotBeNull();
        strategy.GetTrainingMax()!.Value.Should().Be(110m);
    }

    [Fact]
    public void WhenRepsPerSetStrategy_UpdateTrainingMaxValueThrows()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        var act = () => strategy.UpdateTrainingMaxValue(
            TrainingMax.Create(100m, WeightUnit.Kilograms));
        act.Should().Throw<InvalidOperationException>();
    }



    [Fact]
    public void WhenRepsPerSetWithPendingWeight_ConfirmStartingWeightWorks()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable);
        strategy.IsWeightPending.Should().BeTrue();
        strategy.ConfirmStartingWeight(Weight.Create(30m, WeightUnit.Kilograms));
        strategy.IsWeightPending.Should().BeFalse();
        strategy.GetCurrentWeight()!.Value.Should().Be(30m);
    }

    [Fact]
    public void WhenLinearStrategy_ConfirmStartingWeightThrows()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        var act = () => strategy.ConfirmStartingWeight(Weight.Create(30m, WeightUnit.Kilograms));
        act.Should().Throw<InvalidOperationException>();
    }



    [Theory]
    [InlineData(EquipmentType.Barbell, 2.5)]
    [InlineData(EquipmentType.SmithMachine, 2.5)]
    [InlineData(EquipmentType.PlateLoadedMachine, 2.5)]
    [InlineData(EquipmentType.Cable, 2.5)]
    [InlineData(EquipmentType.Machine, 2.5)]
    [InlineData(EquipmentType.Bodyweight, 0)]
    public void WhenRepsPerSetStrategy_GetWeightIncrementMatchesEquipmentType(
        EquipmentType equipment, decimal expectedIncrement)
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, equipment, startingWeight: _weight);
        strategy.GetWeightIncrement().Value.Should().Be(expectedIncrement);
    }

    [Fact]
    public void WhenRepsPerSetDumbbell_GetWeightIncrementIsWeightDependent()
    {
        // Under 10kg → 1kg increment
        var lightWeight = Weight.Create(8m, WeightUnit.Kilograms);
        ExerciseProgression lightStrategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Dumbbell, startingWeight: lightWeight);
        lightStrategy.GetWeightIncrement().Value.Should().Be(1m);

        // Over 10kg → 2kg increment
        var heavyWeight = Weight.Create(12m, WeightUnit.Kilograms);
        ExerciseProgression heavyStrategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Dumbbell, startingWeight: heavyWeight);
        heavyStrategy.GetWeightIncrement().Value.Should().Be(2m);
    }

    [Theory]
    [InlineData(EquipmentType.SmithMachine, 2.5)]
    [InlineData(EquipmentType.PlateLoadedMachine, 2.5)]
    public void WhenMinimalSetsStrategy_GetWeightIncrementForNewEquipmentTypes(
        EquipmentType equipment, decimal expectedIncrement)
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, equipment);
        strategy.GetWeightIncrement().Value.Should().Be(expectedIncrement);
    }

    [Fact]
    public void WhenLinearStrategy_GetWeightIncrementReturnsZero()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        strategy.GetWeightIncrement().Value.Should().Be(0m);
    }



    [Fact]
    public void WhenLinearStrategy_CaptureAndRestoreRoundTrips()
    {
        var strategy = LinearProgressionStrategy.Create(_tm);
        var snapshot = ((ExerciseProgression)strategy).CaptureSnapshot(_exerciseId, "Bench Press");

        snapshot.ProgressionType.Should().Be("Linear");

        var newTm = TrainingMax.Create(120m, WeightUnit.Kilograms);
        strategy.UpdateTrainingMaxValue(newTm);
        strategy.GetTrainingMax()!.Value.Should().Be(120m);

        ((ExerciseProgression)strategy).RestoreFromSnapshot(snapshot);
        strategy.GetTrainingMax()!.Value.Should().Be(100m);
    }

    [Fact]
    public void WhenRepsPerSetStrategy_CaptureAndRestoreRoundTrips()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        var snapshot = ((ExerciseProgression)strategy).CaptureSnapshot(_exerciseId, "Lat Pulldown");

        snapshot.ProgressionType.Should().Be("RepsPerSet");

        strategy.UpdateWeight(Weight.Create(30m, WeightUnit.Kilograms));

        ((ExerciseProgression)strategy).RestoreFromSnapshot(snapshot);
        strategy.GetCurrentWeight()!.Value.Should().Be(20m);
    }

    [Fact]
    public void WhenMinimalSetsStrategy_CaptureAndRestoreRoundTrips()
    {
        var strategy = MinimalSetsStrategy.Create(_weight, 40, 3, EquipmentType.Machine);
        var snapshot = ((ExerciseProgression)strategy).CaptureSnapshot(_exerciseId, "Assisted Dips");

        snapshot.ProgressionType.Should().Be("MinimalSets");

        strategy.UpdateWeight(Weight.Create(30m, WeightUnit.Kilograms));

        ((ExerciseProgression)strategy).RestoreFromSnapshot(snapshot);
        strategy.GetCurrentWeight()!.Value.Should().Be(20m);
    }



    [Fact]
    public void WhenLinearStrategy_GetProgressionDataReturnsLinearFields()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm, useAmrap: true, baseSetsPerExercise: 5);
        var data = strategy.GetProgressionData();

        data.UseAmrap.Should().BeTrue();
        data.BaseSetsPerExercise.Should().Be(5);
        data.RepRangeMinimum.Should().BeNull();
        data.TargetTotalReps.Should().BeNull();
    }

    [Fact]
    public void WhenRepsPerSetStrategy_GetProgressionDataReturnsRepsPerSetFields()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingSets: 2, targetSets: 4, startingWeight: _weight);
        var data = strategy.GetProgressionData();

        data.RepRangeMinimum.Should().Be(8);
        data.RepRangeMaximum.Should().Be(15);
        data.StartingSets.Should().Be(2);
        data.CurrentSetCount.Should().Be(2);
        data.TargetSets.Should().Be(4);
        data.UseAmrap.Should().BeNull();
    }

    [Fact]
    public void WhenMinimalSetsStrategy_GetProgressionDataReturnsMinimalSetsFields()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine, minimumSets: 2, maximumSets: 8);
        var data = strategy.GetProgressionData();

        data.TargetTotalReps.Should().Be(40);
        data.CurrentSetCount.Should().Be(3);
        data.MinimumSets.Should().Be(2);
        data.MaximumSets.Should().Be(8);
        data.RepRangeMinimum.Should().BeNull();
    }



    [Fact]
    public void WhenLinearStrategy_GetProgressionChangeDescriptionReflectsAmrapDelta()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        var performance = CreateLinearPerformance(amrapDelta: 3);
        strategy.GetProgressionChangeDescription(performance).Should().Be("TM increased 1.5%");
    }

    [Fact]
    public void WhenRepsPerSetWithPendingWeight_GetProgressionChangeDescriptionReflectsPending()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(_repRange, EquipmentType.Cable);
        var performance = CreateSimplePerformance(3, 12);
        strategy.GetProgressionChangeDescription(performance).Should().Be("Weight pending confirmation");
    }



    [Fact]
    public void WhenExerciseUsesLinear_PolymorphicMethodsWork()
    {
        var exercise = Exercise.CreateWithLinearProgression(
            "Bench Press", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "bench-press", _tm);

        exercise.GetTrainingMax().Should().NotBeNull();
        exercise.GetCurrentWeight().Should().BeNull();
        exercise.IsUnilateral().Should().BeFalse();
    }

    [Fact]
    public void WhenExerciseUsesRepsPerSet_PolymorphicMethodsWork()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 2, "lat-pulldown", _repRange, startingWeight: _weight);

        exercise.GetTrainingMax().Should().BeNull();
        exercise.GetCurrentWeight().Should().NotBeNull();
        exercise.IsUnilateral().Should().BeFalse();
    }

    [Fact]
    public void WhenExerciseUsesMinimalSets_PolymorphicMethodsWork()
    {
        var exercise = Exercise.CreateWithMinimalSetsProgression(
            "Assisted Dips", ExerciseCategory.Accessory, EquipmentType.Machine,
            DayNumber.Day1, 3, "dips", _weight, 40, 3);

        exercise.GetTrainingMax().Should().BeNull();
        exercise.GetCurrentWeight().Should().NotBeNull();
        exercise.IsUnilateral().Should().BeFalse();
    }

    [Fact]
    public void WhenExerciseUpdateTrainingMax_OnlyWorksForLinear()
    {
        var linearExercise = Exercise.CreateWithLinearProgression(
            "Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "bench", _tm);
        var newTm = TrainingMax.Create(110m, WeightUnit.Kilograms);
        linearExercise.UpdateTrainingMax(newTm).Should().NotBeNull();

        var repsExercise = Exercise.CreateWithRepsPerSetProgression(
            "Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 2, "pulldown", _repRange, startingWeight: _weight);
        var act = () => repsExercise.UpdateTrainingMax(newTm);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WhenExerciseUpdateWeight_OnlyWorksForWeightBased()
    {
        var repsExercise = Exercise.CreateWithRepsPerSetProgression(
            "Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 2, "pulldown", _repRange, startingWeight: _weight);
        repsExercise.UpdateWeight(Weight.Create(25m, WeightUnit.Kilograms));
        repsExercise.GetCurrentWeight()!.Value.Should().Be(25m);

        var linearExercise = Exercise.CreateWithLinearProgression(
            "Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "bench", _tm);
        var act = () => linearExercise.UpdateWeight(Weight.Create(50m, WeightUnit.Kilograms));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WhenExerciseCaptureAndRestoreSnapshot_WorksPolymorphically()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 2, "pulldown", _repRange, startingWeight: _weight);

        var snapshot = exercise.CaptureProgressionSnapshot();
        exercise.UpdateWeight(Weight.Create(30m, WeightUnit.Kilograms));
        exercise.GetCurrentWeight()!.Value.Should().Be(30m);

        exercise.RestoreFromSnapshot(snapshot);
        exercise.GetCurrentWeight()!.Value.Should().Be(20m);
    }



    [Fact]
    public void WhenLinearStrategy_IsWeightPendingIsFalse()
    {
        ExerciseProgression strategy = LinearProgressionStrategy.Create(_tm);
        strategy.IsWeightPending.Should().BeFalse();
    }

    [Fact]
    public void WhenRepsPerSetNoPendingWeight_IsWeightPendingIsFalse()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);
        strategy.IsWeightPending.Should().BeFalse();
    }

    [Fact]
    public void WhenRepsPerSetPendingWeight_IsWeightPendingIsTrue()
    {
        ExerciseProgression strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable);
        strategy.IsWeightPending.Should().BeTrue();
    }

    [Fact]
    public void WhenMinimalSetsStrategy_IsWeightPendingIsFalse()
    {
        ExerciseProgression strategy = MinimalSetsStrategy.Create(
            _weight, 40, 3, EquipmentType.Machine);
        strategy.IsWeightPending.Should().BeFalse();
    }



    private ExercisePerformance CreateLinearPerformance(int amrapDelta)
    {
        var plannedSets = new List<PlannedSet>
        {
            new(1, _weight, 10, false),
            new(2, _weight, 10, false),
            new(3, _weight, 10, false),
            new(4, _weight, 12, true)
        };
        var completedSets = new List<CompletedSet>
        {
            new(1, _weight, 10, false),
            new(2, _weight, 10, false),
            new(3, _weight, 10, false),
            new(4, _weight, 12 + amrapDelta, true)
        };
        return new ExercisePerformance(_exerciseId, plannedSets, completedSets);
    }

    private ExercisePerformance CreateSimplePerformance(int setCount, int reps)
    {
        var planned = Enumerable.Range(1, setCount)
            .Select(i => new PlannedSet(i, _weight, reps, false)).ToList();
        var completed = Enumerable.Range(1, setCount)
            .Select(i => new CompletedSet(i, _weight, reps, false)).ToList();
        return new ExercisePerformance(_exerciseId, planned, completed);
    }

}
