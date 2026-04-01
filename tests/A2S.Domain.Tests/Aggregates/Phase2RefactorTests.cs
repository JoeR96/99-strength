using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Tests for Phase 2 refactoring: aggregate decomposition, immutability,
/// domain events, and encapsulation.
/// </summary>
public class Phase2RefactorTests
{
    private static readonly UserId TestUserId = new("eee11111-1111-1111-1111-111111111111");


    [Fact]
    public void Workout_Create_AcceptsStronglyTypedUserId()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });
    }

    [Fact]
    public void ProgressionAuditEntry_UsesStronglyTypedExerciseId()
    {
        var exerciseId = new ExerciseId(Guid.NewGuid());
        var entry = ProgressionAuditEntry.TemporarySubstitution(
            exerciseId, "Squat", 1, 1);

        entry.ExerciseId.Should().Be(exerciseId);
    }

    [Fact]
    public void ProgressionSnapshot_UsesStronglyTypedExerciseId()
    {
        var exerciseId = new ExerciseId(Guid.NewGuid());
        var snapshot = new ProgressionSnapshot(exerciseId, "Squat", "Linear", "{}");

        snapshot.ExerciseId.Should().Be(exerciseId);
    }

    [Fact]
    public void User_ExtendsAggregateRoot()
    {
        var user = Domain.Entities.User.Reconstitute(Guid.NewGuid().ToString(), "ext-id", "Test User", DateTime.UtcNow);

        user.Id.Should().BeOfType<UserId>();
        user.Should().BeAssignableTo<AggregateRoot<UserId>>();
    }



    [Fact]
    public void Exercise_HasExternalTemplateId()
    {
        var exercise = Exercise.CreateWithLinearProgression(
            "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "my-template-id",
            TrainingMax.Create(100m, WeightUnit.Kilograms), useAmrap: true);

        exercise.ExternalTemplateId.Should().Be("my-template-id");
    }



    [Fact]
    public void WorkoutActivity_Performances_IsReadOnly()
    {
        var performance = CreatePerformance(new ExerciseId(Guid.NewGuid()));
        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, new[] { performance });

        activity.Performances.Should().BeAssignableTo<IReadOnlyList<ExercisePerformance>>();
    }

    [Fact]
    public void WorkoutActivity_ProgressionSnapshots_IsReadOnly()
    {
        var performance = CreatePerformance(new ExerciseId(Guid.NewGuid()));
        var snapshot = new ProgressionSnapshot(new ExerciseId(Guid.NewGuid()), "Test", "Linear", "{}");
        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, new[] { performance }, new[] { snapshot });

        activity.ProgressionSnapshots.Should().BeAssignableTo<IReadOnlyList<ProgressionSnapshot>>();
    }

    [Fact]
    public void WorkoutActivity_WithReplacedSnapshot_ReturnsNewInstance()
    {
        var exerciseId = new ExerciseId(Guid.NewGuid());
        var performance = CreatePerformance(exerciseId);
        var originalSnapshot = new ProgressionSnapshot(exerciseId, "Test", "Linear", "{\"old\":true}");
        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, new[] { performance }, new[] { originalSnapshot });

        var replacement = new ProgressionSnapshot(exerciseId, "Test", "Linear", "{\"new\":true}");
        var updated = activity.WithReplacedSnapshot(0, replacement);

        updated.Should().NotBeSameAs(activity);
        updated.ProgressionSnapshots[0].ProgressionStateJson.Should().Contain("new");
        activity.ProgressionSnapshots[0].ProgressionStateJson.Should().Contain("old");
    }

    [Fact]
    public void ExercisePerformance_CompletedSets_IsReadOnly()
    {
        var performance = CreatePerformance(new ExerciseId(Guid.NewGuid()));

        performance.CompletedSets.Should().BeAssignableTo<IReadOnlyList<CompletedSet>>();
        performance.PlannedSets.Should().BeAssignableTo<IReadOnlyList<PlannedSet>>();
    }



    [Fact]
    public void ConfirmExerciseStartingWeight_RoutedThroughAggregate()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 1, "LP1", RepRange.Create(8, 12));
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });
        workout.Start();

        var weight = Weight.Create(50m, WeightUnit.Kilograms);
        workout.ConfirmExerciseStartingWeight(exercise.Id, weight);

        var ex = workout.GetExerciseById(exercise.Id)!;
        var rps = (RepsPerSetStrategy)ex.Progression;
        rps.CurrentWeight!.Value.Should().Be(50m);
    }

    [Fact]
    public void UpdateExerciseWorkingWeight_RoutedThroughAggregate()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 1, "LP1", RepRange.Create(8, 12),
            startingWeight: Weight.Create(40m, WeightUnit.Kilograms));
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });
        workout.Start();

        var weight = Weight.Create(45m, WeightUnit.Kilograms);
        workout.UpdateExerciseWorkingWeight(exercise.Id, weight);

        var ex = workout.GetExerciseById(exercise.Id)!;
        var rps = (RepsPerSetStrategy)ex.Progression;
        rps.CurrentWeight!.Value.Should().Be(45m);
    }

    [Fact]
    public void UpdateExerciseWorkingWeight_RejectsLinearProgression()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });
        workout.Start();

        var weight = Weight.Create(100m, WeightUnit.Kilograms);
        var act = () => workout.UpdateExerciseWorkingWeight(exercise.Id, weight);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*linear progression*");
    }

    [Fact]
    public void ConfirmExerciseStartingWeight_ThrowsForUnknownExercise()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });

        var act = () => workout.ConfirmExerciseStartingWeight(
            new ExerciseId(Guid.NewGuid()), Weight.Create(100m, WeightUnit.Kilograms));

        act.Should().Throw<InvalidOperationException>();
    }



    [Fact]
    public void CompleteDay_RaisesTrainingMaxAdjustedEvents()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay,
            new[] { exercise });
        workout.Start();
        workout.ClearDomainEvents();

        var plannedSets = workout.GetPlannedSetsForDay(DayNumber.Day1).ToList();
        var completedSets = plannedSets.Select(ps =>
            new CompletedSet(ps.SetNumber, ps.Weight, ps.TargetReps + 3, ps.IsAmrap))
            .ToList();

        var performance = new ExercisePerformance(exercise.Id, plannedSets, completedSets);
        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.OfType<TrainingMaxAdjusted>().Should().HaveCount(1);
    }

    [Fact]
    public void CompleteDay_WithSkipProgression_DoesNotRaiseTrainingMaxAdjusted()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay,
            new[] { exercise });
        workout.Start();
        workout.ClearDomainEvents();

        var plannedSets = workout.GetPlannedSetsForDay(DayNumber.Day1).ToList();
        var completedSets = plannedSets.Select(ps =>
            new CompletedSet(ps.SetNumber, ps.Weight, ps.TargetReps + 3, ps.IsAmrap))
            .ToList();

        var performance = new ExercisePerformance(exercise.Id, plannedSets, completedSets, skipProgression: true);
        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.OfType<TrainingMaxAdjusted>().Should().BeEmpty();
        workout.DomainEvents.OfType<ProgressionSkipped>().Should().HaveCount(1);
    }



    [Fact]
    public void CompleteDay_RecordsActivityWithSnapshots()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay,
            new[] { exercise });
        workout.Start();

        CompleteDay(workout, exercise, DayNumber.Day1);

        workout.CompletedActivities.Should().HaveCount(1);
        var activity = workout.CompletedActivities.First();
        activity.ProgressionSnapshots.Should().HaveCount(1);
        activity.ProgressionSnapshots[0].ExerciseId.Should().Be(exercise.Id);
    }

    [Fact]
    public void CompleteDay_RejectsDuplicateDay()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay,
            new[] { exercise });
        workout.Start();

        CompleteDay(workout, exercise, DayNumber.Day1);

        var act = () => CompleteDay(workout, exercise, DayNumber.Day1);
        act.Should().Throw<DomainException>();
    }



    [Fact]
    public void ProgressAfterDayCompletion_AdvancesWeekWhenAllDaysComplete()
    {
        var exercises = new[]
        {
            CreateLinearExercise("Squat", DayNumber.Day1),
            CreateLinearExercise("Bench", DayNumber.Day2),
            CreateLinearExercise("Row", DayNumber.Day3),
            CreateLinearExercise("OHP", DayNumber.Day4),
        };
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, exercises);
        workout.Start();

        CompleteDay(workout, exercises[0], DayNumber.Day1);
        CompleteDay(workout, exercises[1], DayNumber.Day2);
        CompleteDay(workout, exercises[2], DayNumber.Day3);
        workout.ClearDomainEvents();

        CompleteDay(workout, exercises[3], DayNumber.Day4);

        workout.CurrentWeek.Should().Be(2);
        workout.CurrentDay.Should().Be(1);
        workout.DomainEvents.OfType<WeekProgressed>().Should().HaveCount(1);
    }



    [Fact]
    public void CaptureProgressionSnapshot_Linear_ProducesValidJson()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var snapshot = exercise.CaptureProgressionSnapshot();

        snapshot.ProgressionType.Should().Be("Linear");
        var state = snapshot.GetLinearState();
        state.Should().NotBeNull();
        state!.TrainingMaxValue.Should().Be(100m);
        state.UseAmrap.Should().BeTrue();
    }

    [Fact]
    public void CaptureProgressionSnapshot_RepsPerSet_ProducesValidJson()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 1, "LP1", RepRange.Create(8, 12),
            startingWeight: Weight.Create(50m, WeightUnit.Kilograms));

        var snapshot = exercise.CaptureProgressionSnapshot();

        snapshot.ProgressionType.Should().Be("RepsPerSet");
        var state = snapshot.GetRepsPerSetState();
        state.Should().NotBeNull();
        state!.CurrentWeight.Should().Be(50m);
        state.IsUnilateral.Should().BeFalse();
    }

    [Fact]
    public void CaptureProgressionSnapshot_MinimalSets_ProducesValidJson()
    {
        var exercise = Exercise.CreateWithMinimalSetsProgression(
            "Bicep Curl", ExerciseCategory.Accessory, EquipmentType.Dumbbell,
            DayNumber.Day1, 1, "BC1", Weight.Create(15m, WeightUnit.Kilograms),
            minimumSets: 2, maximumSets: 4, startingSets: 2, targetTotalReps: 25);

        var snapshot = exercise.CaptureProgressionSnapshot();

        snapshot.ProgressionType.Should().Be("MinimalSets");
        var state = snapshot.GetMinimalSetsState();
        state.Should().NotBeNull();
        state!.CurrentWeight.Should().Be(15m);
        state.MinimumSets.Should().Be(2);
        state.MaximumSets.Should().Be(4);
    }

    [Fact]
    public void RestoreFromSnapshot_Linear_RestoresState()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var originalTm = ((LinearProgressionStrategy)exercise.Progression).TrainingMax.Value;

        // Capture snapshot
        var snapshot = exercise.CaptureProgressionSnapshot();

        // Change state
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });
        workout.Start();
        CompleteDay(workout, exercise, DayNumber.Day1);

        // TM should have changed after progression
        var changedTm = ((LinearProgressionStrategy)exercise.Progression).TrainingMax.Value;
        changedTm.Should().NotBe(originalTm);

        // Restore and verify original state
        exercise.RestoreFromSnapshot(snapshot);
        ((LinearProgressionStrategy)exercise.Progression).TrainingMax.Value.Should().Be(originalTm);
    }



    [Fact]
    public void SetExerciseTrainingMax_UpdatesTrainingMax()
    {
        var exercise = CreateLinearExercise("Squat", DayNumber.Day1);
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });

        var newTm = TrainingMax.Create(110m, WeightUnit.Kilograms);
        workout.SetExerciseTrainingMax(exercise.Id, newTm);

        var linear = (LinearProgressionStrategy)exercise.Progression;
        linear.TrainingMax.Value.Should().Be(110m);
    }

    [Fact]
    public void SetExerciseTrainingMax_ThrowsForNonLinearExercise()
    {
        var exercise = Exercise.CreateWithRepsPerSetProgression(
            "Curl", ExerciseCategory.Accessory, EquipmentType.Dumbbell,
            DayNumber.Day1, 1, "C1", RepRange.Create(8, 12),
            startingWeight: Weight.Create(10m, WeightUnit.Kilograms));
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, new[] { exercise });

        var act = () => workout.SetExerciseTrainingMax(
            exercise.Id, TrainingMax.Create(50m, WeightUnit.Kilograms));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ProgressionAuditEntry_UndoCompletion_HasNullExerciseId()
    {
        var entry = ProgressionAuditEntry.UndoCompletion(1, 1);

        entry.ExerciseId.Should().BeNull();
        entry.ExerciseName.Should().Be("N/A");
    }

    [Fact]
    public void User_Create_WithExplicitId_UsesProvidedId()
    {
        var userId = new UserId("aaa11111-1111-1111-1111-111111111111");
        var user = Domain.Entities.User.Create("test@example.com", "Test", userId);

        user.Id.Should().Be(userId);
    }

    [Fact]
    public void User_Create_WithoutId_GeneratesNewId()
    {
        var user = Domain.Entities.User.Create("test@example.com", "Test");

        user.Id.Value.Should().NotBeNullOrEmpty();
    }


    private static Exercise CreateLinearExercise(string name, DayNumber day)
    {
        return Exercise.CreateWithLinearProgression(
            name, ExerciseCategory.MainLift, EquipmentType.Barbell,
            day, 1, $"{name}-template",
            TrainingMax.Create(100m, WeightUnit.Kilograms), useAmrap: true);
    }

    private static void CompleteDay(Workout workout, Exercise exercise, DayNumber day)
    {
        var plannedSets = workout.GetPlannedSetsForDay(day).ToList();
        var completedSets = plannedSets.Select(ps =>
            new CompletedSet(ps.SetNumber, ps.Weight, ps.TargetReps + 2, ps.IsAmrap))
            .ToList();
        var performance = new ExercisePerformance(exercise.Id, plannedSets, completedSets);
        workout.CompleteDay(day, new[] { performance });
    }

    private static ExercisePerformance CreatePerformance(ExerciseId exerciseId)
    {
        var planned = new[] { new PlannedSet(1, Weight.Create(100m, WeightUnit.Kilograms), 5, false) };
        var completed = new[] { new CompletedSet(1, Weight.Create(100m, WeightUnit.Kilograms), 5, false) };
        return new ExercisePerformance(exerciseId, planned, completed);
    }

}
