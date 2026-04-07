using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Phase 7 tests: weight confirmation flow for Cable/Machine exercises,
/// SmithMachine/PlateLoadedMachine weight increment verification,
/// and ExerciseDefinition entity tests.
/// </summary>
public class WeightConfirmationTests
{
    private readonly ExerciseId _exerciseId = new(Guid.Parse("eee66666-6666-6666-6666-666666666666"));
    private readonly Weight _startingWeight = Weight.Create(20m, WeightUnit.Kilograms);
    private readonly RepRange _repRange = RepRange.Create(8, 15);


    [Fact]
    public void WhenCableExerciseProgressesWeight_PendingWeightConfirmationIsTrue()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        // Advance to max sets
        AdvanceToMaxSets(strategy, 3);

        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        strategy.PendingWeightConfirmation.Should().BeTrue();
        strategy.SuggestedWeight.Should().NotBeNull();
        strategy.SuggestedWeight!.Value.Should().Be(22.5m); // 20 + 2.5
        strategy.CurrentWeight!.Value.Should().Be(22.5m); // Weight is already applied
        strategy.CurrentSetCount.Should().Be(2); // Reset to starting sets
    }

    [Fact]
    public void WhenMachineExerciseProgressesWeight_PendingWeightConfirmationIsTrue()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Machine,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        AdvanceToMaxSets(strategy, 3);

        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        strategy.PendingWeightConfirmation.Should().BeTrue();
        strategy.SuggestedWeight!.Value.Should().Be(22.5m);
    }

    [Fact]
    public void WhenBarbellExerciseProgressesWeight_NoPendingWeightConfirmation()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Barbell,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        AdvanceToMaxSets(strategy, 3);

        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        strategy.PendingWeightConfirmation.Should().BeFalse();
        strategy.SuggestedWeight.Should().BeNull();
        strategy.CurrentWeight!.Value.Should().Be(22.5m); // Weight increased normally
    }

    [Fact]
    public void WhenSmithMachineExerciseProgressesWeight_NoPendingWeightConfirmation()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.SmithMachine,
            startingSets: 2, targetSets: 3,
            startingWeight: Weight.Create(40m, WeightUnit.Kilograms));

        AdvanceToMaxSets(strategy, 3);

        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount,
            Weight.Create(40m, WeightUnit.Kilograms));
        strategy.ApplyPerformanceResult(performance);

        strategy.PendingWeightConfirmation.Should().BeFalse();
        strategy.CurrentWeight!.Value.Should().Be(42.5m);
    }


    [Fact]
    public void WhenConfirmWorkingWeight_ClearsPendingAndSetsWeight()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        // Trigger weight progression
        AdvanceToMaxSets(strategy, 3);
        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);
        strategy.PendingWeightConfirmation.Should().BeTrue();

        var confirmedWeight = Weight.Create(25m, WeightUnit.Kilograms);
        strategy.ConfirmWorkingWeight(confirmedWeight);

        strategy.PendingWeightConfirmation.Should().BeFalse();
        strategy.SuggestedWeight.Should().BeNull();
        strategy.CurrentWeight!.Value.Should().Be(25m);
    }

    [Fact]
    public void WhenConfirmWorkingWeight_WithSuggestedWeight_AcceptsSuggestion()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        AdvanceToMaxSets(strategy, 3);
        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        strategy.ConfirmWorkingWeight(strategy.SuggestedWeight!);

        strategy.PendingWeightConfirmation.Should().BeFalse();
        strategy.CurrentWeight!.Value.Should().Be(22.5m);
    }

    [Fact]
    public void WhenConfirmWorkingWeight_NoPendingConfirmation_Throws()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        var act = () => strategy.ConfirmWorkingWeight(Weight.Create(25m, WeightUnit.Kilograms));

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void WhenConfirmWorkingWeight_WrongUnit_Throws()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        AdvanceToMaxSets(strategy, 3);
        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        var act = () => strategy.ConfirmWorkingWeight(Weight.Create(55m, WeightUnit.Pounds));

        act.Should().Throw<BusinessRuleViolationException>();
    }


    [Fact]
    public void WhenExerciseConfirmWorkingWeight_DelegatesToProgression()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Cable Row", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 1, "cable-row",
            _repRange, startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        // Trigger weight progression via strategy
        var rpsStrategy = (RepsPerSetStrategy)exercise.Progression;
        AdvanceToMaxSets(rpsStrategy, 3);
        var performance = CreateAllMaxPerformance(rpsStrategy.CurrentSetCount, _startingWeight, exercise.Id);
        exercise.ApplyProgression(performance);

        exercise.Progression.PendingWeightConfirmation.Should().BeTrue();

        exercise.ConfirmWorkingWeight(Weight.Create(25m, WeightUnit.Kilograms));

        exercise.Progression.PendingWeightConfirmation.Should().BeFalse();
        exercise.Progression.GetCurrentWeight()!.Value.Should().Be(25m);
    }

    [Fact]
    public void WhenLinearExercise_ConfirmWorkingWeightThrows()
    {
        var exercise = Exercise.CreateWithLinearProgression(
            "Bench Press", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "bench-press",
            TrainingMax.Create(100m, WeightUnit.Kilograms));

        var act = () => exercise.ConfirmWorkingWeight(Weight.Create(100m, WeightUnit.Kilograms));

        act.Should().Throw<InvalidOperationException>();
    }


    [Fact]
    public void WhenCaptureAndRestore_PendingWeightIsPreserved()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 3,
            startingWeight: _startingWeight);

        AdvanceToMaxSets(strategy, 3);
        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        // Capture state
        var state = strategy.CaptureState();

        // Modify strategy
        strategy.ConfirmWorkingWeight(Weight.Create(25m, WeightUnit.Kilograms));
        strategy.PendingWeightConfirmation.Should().BeFalse();

        // Restore
        strategy.RestoreFromState(state);

        strategy.PendingWeightConfirmation.Should().BeTrue();
        strategy.SuggestedWeight!.Value.Should().Be(22.5m);
        strategy.CurrentWeight!.Value.Should().Be(22.5m);
    }


    [Fact]
    public void WhenCableExerciseAddsSet_NoPendingWeightConfirmation()
    {
        // Adding sets shouldn't trigger weight confirmation
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 2, targetSets: 5,
            startingWeight: _startingWeight);

        var performance = CreateAllMaxPerformance(strategy.CurrentSetCount, _startingWeight);
        strategy.ApplyPerformanceResult(performance);

        // Set added, not weight increased
        strategy.CurrentSetCount.Should().Be(3);
        strategy.PendingWeightConfirmation.Should().BeFalse();
    }


    [Fact]
    public void WhenCreateExerciseDefinition_WithValidData_Succeeds()
    {
        var definition = new A2S.Domain.Entities.ExerciseDefinition(
            new A2S.Domain.Common.ExerciseDefinitionId(Guid.NewGuid()),
            "Bench Press (Barbell)",
            EquipmentType.Barbell,
            "chest",
            isCompound: true,
            description: "Hevy: chest",
            defaultRepRangeMin: 8,
            defaultRepRangeMax: 12,
            defaultSets: 3);

        definition.Name.Should().Be("Bench Press (Barbell)");
        definition.EquipmentType.Should().Be(EquipmentType.Barbell);
        definition.MuscleGroup.Should().Be("chest");
        definition.IsCompound.Should().BeTrue();
        definition.DefaultRepRangeMin.Should().Be(8);
        definition.DefaultRepRangeMax.Should().Be(12);
        definition.DefaultSets.Should().Be(3);
    }

    [Fact]
    public void WhenCreateExerciseDefinition_WithEmptyName_Throws()
    {
        var act = () => new A2S.Domain.Entities.ExerciseDefinition(
            new A2S.Domain.Common.ExerciseDefinitionId(Guid.NewGuid()),
            "",
            EquipmentType.Barbell,
            "chest",
            isCompound: true,
            description: "");

        act.Should().Throw<BusinessRuleViolationException>();
    }


    private static void AdvanceToMaxSets(RepsPerSetStrategy strategy, int targetSets)
    {
        while (strategy.CurrentSetCount < targetSets)
        {
            var perf = CreateAllMaxPerformance(strategy.CurrentSetCount,
                strategy.CurrentWeight!);
            strategy.ApplyPerformanceResult(perf);
        }
    }

    private static ExercisePerformance CreateAllMaxPerformance(
        int setCount, Weight weight, ExerciseId? exerciseId = null)
    {
        var id = exerciseId ?? new ExerciseId(Guid.NewGuid());

        var planned = Enumerable.Range(1, setCount)
            .Select(i => new PlannedSet(i, weight, 15, false))
            .ToList();

        var completed = Enumerable.Range(1, setCount)
            .Select(i => new CompletedSet(i, weight, 15, false))
            .ToList();

        return new ExercisePerformance(id, planned, completed, skipProgression: false);
    }

}
