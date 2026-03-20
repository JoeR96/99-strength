using A2S.Application.Commands.CompleteDay;
using A2S.Application.DTOs;
using A2S.Domain.Enums;

namespace A2S.Tests.Shared.Helpers;

/// <summary>
/// Shared test helper methods for creating exercise performance requests.
/// Eliminates duplication across integration test files.
/// </summary>
public static class PerformanceRequestBuilders
{
    /// <summary>
    /// Creates a "maintain" performance — hits target but doesn't trigger progression changes.
    /// Supports all 3 progression types: Linear, RepsPerSet, MinimalSets.
    /// </summary>
    public static List<CompletedSetRequest> CreateMaintainPerformance(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression is LinearProgressionDto linear)
        {
            var weightUnit = linear.TrainingMax.Unit == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = linear.TrainingMax.Value * 0.7m,
                    WeightUnit = weightUnit,
                    ActualReps = 10,
                    WasAmrap = i == linear.BaseSetsPerExercise
                });
            }
        }
        else if (exercise.Progression is RepsPerSetProgressionDto rps)
        {
            var weightUnit = rps.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= rps.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = rps.RepRange.Target,
                    WasAmrap = false
                });
            }
        }
        else if (exercise.Progression is MinimalSetsProgressionDto minimal)
        {
            var repsPerSet = minimal.TargetTotalReps / minimal.CurrentSetCount;
            var remainder = minimal.TargetTotalReps % minimal.CurrentSetCount;
            for (int i = 1; i <= minimal.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = WeightUnit.Kilograms,
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    /// <summary>
    /// Creates a performance request for a Linear exercise with week-specific AMRAP delta.
    /// </summary>
    /// <param name="exercise">The exercise DTO with LinearProgressionDto.</param>
    /// <param name="week">Current week number (1-21).</param>
    /// <param name="isDeload">Whether the current week is a deload week.</param>
    /// <param name="delta">AMRAP delta (actual reps - target reps).</param>
    /// <param name="setCount">Number of sets to generate.</param>
    /// <param name="skipAmrapOnDeload">If true, WasAmrap is false on deload weeks (WorkoutFlow behavior).</param>
    public static ExercisePerformanceRequest CreateLinearPerformance(
        ExerciseDto exercise, int week, bool isDeload, int delta,
        int setCount, bool skipAmrapOnDeload = false)
    {
        var linear = exercise.Progression as LinearProgressionDto;
        var weightUnit = linear!.TrainingMax.Unit == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds;
        var repsPerSet = ProgramWeekHelpers.GetRepsPerSetForWeek(week);
        var repOutTarget = ProgramWeekHelpers.GetRepOutTargetForWeek(week);
        var intensity = ProgramWeekHelpers.GetIntensityForWeek(week);

        var setsList = new List<CompletedSetRequest>();
        for (int i = 1; i <= setCount; i++)
        {
            var isLastSet = i == setCount;
            var wasAmrap = skipAmrapOnDeload
                ? isLastSet && !isDeload
                : isLastSet;
            var actualReps = isLastSet && !isDeload
                ? Math.Max(1, repOutTarget + delta)
                : repsPerSet;

            setsList.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = linear.TrainingMax.Value * intensity,
                WeightUnit = weightUnit,
                ActualReps = actualReps,
                WasAmrap = wasAmrap
            });
        }

        return new ExercisePerformanceRequest
        {
            ExerciseId = exercise.Id,
            CompletedSets = setsList
        };
    }
}
