using System.Text.Json.Serialization;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Type of weight confirmation needed after completing a training day.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfirmationType
{
    StartingWeight,
    WorkingWeight
}

/// <summary>
/// Result of completing a training day.
/// </summary>
public sealed record CompleteDayResult
{
    public required DayNumber Day { get; init; }
    public required int WeekNumber { get; init; }
    public required int BlockNumber { get; init; }
    public required int ExercisesCompleted { get; init; }
    public required IReadOnlyList<ProgressionChangeDto> ProgressionChanges { get; init; }
    public required int NewCurrentWeek { get; init; }
    public required int NewCurrentDay { get; init; }
    public required bool WeekProgressed { get; init; }
    public required bool ProgramComplete { get; init; }
    public bool IsDeloadWeek { get; init; }
    public IReadOnlyList<PendingWeightExerciseDto> ExercisesPendingWeightConfirmation { get; init; } = [];
}

/// <summary>
/// Exercise that needs weight confirmation (starting or working weight).
/// </summary>
public sealed record PendingWeightExerciseDto
{
    public required Guid ExerciseId { get; init; }
    public required string ExerciseName { get; init; }
    public required decimal SuggestedWeight { get; init; }
    public required string WeightUnit { get; init; }

    /// <summary>
    /// Type of weight confirmation needed: StartingWeight or WorkingWeight.
    /// </summary>
    public required ConfirmationType ConfirmationType { get; init; }
}

/// <summary>
/// Summary of a progression change for an exercise.
/// </summary>
public sealed record ProgressionChangeDto
{
    public required Guid ExerciseId { get; init; }
    public required string ExerciseName { get; init; }
    public required string Change { get; init; }
}
