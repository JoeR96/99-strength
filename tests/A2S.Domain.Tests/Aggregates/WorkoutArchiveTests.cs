using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Tests for the archive behavior when restarting a completed program
/// via UpdateBlockSequence.
/// </summary>
public class WorkoutArchiveTests
{
    /// <summary>
    /// Creates a workout with a single block (7 weeks) and completes all days
    /// so the workout reaches Completed status, with activities to archive.
    /// </summary>
    private static Workout CreateCompletedWorkout()
    {
        var exercises = new[]
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "SQ1",
                TrainingMax.Create(100m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day2, 1, "BP1",
                TrainingMax.Create(80m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "Deadlift", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day3, 1, "DL1",
                TrainingMax.Create(120m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "OHP", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day4, 1, "OH1",
                TrainingMax.Create(60m, WeightUnit.Kilograms), useAmrap: true),
        };

        // Use a single block [1] = 7 weeks, FourDay variant = 4 days/week
        var workout = Workout.Create(
            new UserId(Guid.NewGuid().ToString()),
            "Test Workout",
            ProgramVariant.FourDay,
            exercises,
            new List<int> { 1 });

        workout.Start();

        // Complete all 7 weeks × 4 days = 28 day completions
        CompleteAllDays(workout, exercises, 7, 4);

        // Workout should be completed after all 28 days
        workout.Status.Should().Be(WorkoutStatus.Completed);
        return workout;
    }

    [Fact]
    public void UpdateBlockSequence_WhenCompleted_MovesActivitiesToArchive()
    {
        // Arrange
        var workout = CreateCompletedWorkout();
        var originalActivityCount = workout.CompletedActivities.Count;
        originalActivityCount.Should().Be(28);

        // Act
        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        // Assert
        workout.ArchivedActivities.Should().HaveCount(28);
        workout.CompletedActivities.Should().BeEmpty();
    }

    [Fact]
    public void UpdateBlockSequence_WhenCompleted_ClearsCompletedActivities()
    {
        // Arrange
        var workout = CreateCompletedWorkout();

        // Act
        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        // Assert
        workout.CompletedActivities.Should().BeEmpty();
    }

    [Fact]
    public void UpdateBlockSequence_WhenCompleted_RaisesProgramRestartedEvent()
    {
        // Arrange
        var workout = CreateCompletedWorkout();
        workout.ClearDomainEvents(); // Clear events from creation/completion

        // Act
        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        // Assert
        var restartedEvent = workout.DomainEvents
            .OfType<ProgramRestarted>()
            .Should().ContainSingle()
            .Which;

        restartedEvent.WorkoutId.Should().Be(workout.Id);
        restartedEvent.ArchivedActivitiesCount.Should().Be(28);
    }

    [Fact]
    public void UpdateBlockSequence_WhenCompleted_ResetsWorkoutState()
    {
        // Arrange
        var workout = CreateCompletedWorkout();

        // Act
        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        // Assert
        workout.Status.Should().Be(WorkoutStatus.Active);
        workout.CurrentWeek.Should().Be(1);
        workout.CurrentDay.Should().Be(1);
        workout.CompletedAt.Should().BeNull();
        workout.TotalWeeks.Should().Be(21);
    }

    [Fact]
    public void UpdateBlockSequence_WhenCompletedTwice_AccumulatesArchivedActivities()
    {
        // Arrange - complete workout once, restart, complete again, restart again
        var exercises = new[]
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "SQ1",
                TrainingMax.Create(100m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day2, 1, "BP1",
                TrainingMax.Create(80m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "Deadlift", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day3, 1, "DL1",
                TrainingMax.Create(120m, WeightUnit.Kilograms), useAmrap: true),
            Exercise.CreateWithLinearProgression(
                "OHP", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day4, 1, "OH1",
                TrainingMax.Create(60m, WeightUnit.Kilograms), useAmrap: true),
        };

        var workout = Workout.Create(
            new UserId(Guid.NewGuid().ToString()),
            "Test Workout",
            ProgramVariant.FourDay,
            exercises,
            new List<int> { 1 });

        workout.Start();

        // Complete first cycle (7 weeks × 4 days)
        CompleteAllDays(workout, exercises, 7, 4);
        workout.Status.Should().Be(WorkoutStatus.Completed);

        // Restart
        workout.UpdateBlockSequence(new List<int> { 1 });
        workout.ArchivedActivities.Should().HaveCount(28);

        // Complete second cycle
        CompleteAllDays(workout, exercises, 7, 4);
        workout.Status.Should().Be(WorkoutStatus.Completed);

        // Restart again
        workout.UpdateBlockSequence(new List<int> { 1 });

        // Assert - should have 56 archived activities (28 from each cycle)
        workout.ArchivedActivities.Should().HaveCount(56);
        workout.CompletedActivities.Should().BeEmpty();
    }

    private static void CompleteAllDays(Workout workout, Exercise[] exercises, int weeks, int daysPerWeek)
    {
        for (var week = 1; week <= weeks; week++)
        {
            for (var day = 1; day <= daysPerWeek; day++)
            {
                var dayNumber = (DayNumber)day;
                var exercise = exercises[day - 1];
                var plannedSets = workout.GetPlannedSetsForDay(dayNumber).ToList();

                var completedSets = plannedSets.Select(ps =>
                    new CompletedSet(ps.SetNumber, ps.Weight, ps.TargetReps + 2, ps.IsAmrap))
                    .ToList();

                var performance = new ExercisePerformance(
                    exercise.Id,
                    plannedSets,
                    completedSets);

                workout.CompleteDay(dayNumber, new[] { performance });
            }
        }
    }
}
