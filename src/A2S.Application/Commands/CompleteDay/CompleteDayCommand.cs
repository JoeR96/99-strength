using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Command to complete a training day with exercise performance data.
/// This applies progression rules to each exercise based on actual performance.
/// </summary>
public sealed record CompleteDayCommand(
    Guid WorkoutId,
    DayNumber Day,
    IReadOnlyList<ExercisePerformanceRequest> Performances
) : ICommand<Result<CompleteDayResult>>;

/// <summary>
/// Request data for a single exercise's performance.
/// </summary>
public sealed record ExercisePerformanceRequest
{
    /// <summary>
    /// The ID of the exercise that was performed.
    /// </summary>
    public required Guid ExerciseId { get; init; }

    /// <summary>
    /// The sets completed for this exercise.
    /// </summary>
    public required IReadOnlyList<CompletedSetRequest> CompletedSets { get; init; }
}

/// <summary>
/// Request data for a single completed set.
/// </summary>
public sealed record CompletedSetRequest
{
    /// <summary>
    /// The set number (1-based).
    /// </summary>
    public required int SetNumber { get; init; }

    /// <summary>
    /// The weight used for this set.
    /// </summary>
    public required decimal Weight { get; init; }

    /// <summary>
    /// The unit of the weight (Kilograms or Pounds).
    /// </summary>
    public required WeightUnit WeightUnit { get; init; }

    /// <summary>
    /// The actual reps completed for this set.
    /// </summary>
    public required int ActualReps { get; init; }

    /// <summary>
    /// Whether this was an AMRAP (As Many Reps As Possible) set.
    /// </summary>
    public bool WasAmrap { get; init; }
}

/// <summary>
/// Result of completing a training day.
/// </summary>
public sealed record CompleteDayResult
{
    /// <summary>
    /// The day that was completed.
    /// </summary>
    public required DayNumber Day { get; init; }

    /// <summary>
    /// The week number when this day was completed.
    /// </summary>
    public required int WeekNumber { get; init; }

    /// <summary>
    /// The block number when this day was completed.
    /// </summary>
    public required int BlockNumber { get; init; }

    /// <summary>
    /// Number of exercises completed.
    /// </summary>
    public required int ExercisesCompleted { get; init; }

    /// <summary>
    /// Summary of progression changes applied.
    /// </summary>
    public required IReadOnlyList<ProgressionChangeDto> ProgressionChanges { get; init; }

    /// <summary>
    /// The new current week after completing this day.
    /// May be incremented if all days in the week were completed.
    /// </summary>
    public required int NewCurrentWeek { get; init; }

    /// <summary>
    /// The new current day after completing this day.
    /// </summary>
    public required int NewCurrentDay { get; init; }

    /// <summary>
    /// Whether the week was completed and progressed to the next.
    /// </summary>
    public required bool WeekProgressed { get; init; }

    /// <summary>
    /// Whether this was the final workout and the program is complete.
    /// </summary>
    public required bool ProgramComplete { get; init; }

    /// <summary>
    /// Whether the new week is a deload week (if week progressed).
    /// </summary>
    public bool IsDeloadWeek { get; init; }
}

/// <summary>
/// Summary of a progression change for an exercise.
/// </summary>
public sealed record ProgressionChangeDto
{
    /// <summary>
    /// The exercise ID.
    /// </summary>
    public required Guid ExerciseId { get; init; }

    /// <summary>
    /// The exercise name.
    /// </summary>
    public required string ExerciseName { get; init; }

    /// <summary>
    /// Description of the change (e.g., "TM increased 2%", "Added 1 set", "No change").
    /// </summary>
    public required string Change { get; init; }
}
