using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.Commands.UpdateExercises;
using A2S.Domain.Enums;

namespace A2S.Api.Contracts.Requests;

/// <summary>
/// Request body for completing a training day.
/// </summary>
public sealed record CompleteDayRequest
{
    /// <summary>
    /// The exercise performances for this day.
    /// </summary>
    public required IReadOnlyList<ExercisePerformanceRequest> Performances { get; init; }
}

/// <summary>
/// Request body for updating exercises.
/// </summary>
public sealed record UpdateExercisesRequest
{
    /// <summary>
    /// The list of exercise updates to apply.
    /// </summary>
    public required IReadOnlyList<ExerciseUpdateRequest> Updates { get; init; }
}

/// <summary>
/// Request body for substituting an exercise.
/// </summary>
public sealed record SubstituteExerciseRequest
{
    /// <summary>
    /// The new exercise name to substitute with.
    /// </summary>
    public required string NewExerciseName { get; init; }

    /// <summary>
    /// Optional new Hevy exercise template ID.
    /// </summary>
    public string? NewExternalTemplateId { get; init; }

    /// <summary>
    /// Optional reason for the substitution.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Optional new progression configuration.
    /// When provided, the exercise's progression type will be changed.
    /// </summary>
    public ProgressionConfigDto? NewProgressionConfig { get; init; }
}

/// <summary>
/// Request body for updating working weight.
/// </summary>
public sealed record UpdateWorkingWeightRequest
{
    /// <summary>
    /// The new weight value.
    /// </summary>
    public required decimal NewWeight { get; init; }

    /// <summary>
    /// The weight unit (Kilograms or Pounds).
    /// </summary>
    public required WeightUnit Unit { get; init; }

    /// <summary>
    /// Optional reason for the weight update.
    /// </summary>
    public string? Reason { get; init; }
}

public sealed record ConfirmStartingWeightRequest
{
    public required decimal Weight { get; init; }
    public required WeightUnit Unit { get; init; }
}

/// <summary>
/// Request body for confirming a new working weight after Cable/Machine progression.
/// </summary>
public sealed record ConfirmWorkingWeightRequest
{
    public required decimal Weight { get; init; }
    public required WeightUnit Unit { get; init; }
}

/// <summary>
/// Request body for updating block sequence.
/// </summary>
public sealed record UpdateBlockSequenceRequest
{
    public required List<int> BlockSequence { get; init; }
}

/// <summary>
/// Request body for syncing a routine to Hevy.
/// </summary>
public sealed record SyncRoutineRequest
{
    public Guid WorkoutId { get; init; }
    public int WeekNumber { get; init; }
    public int DayNumber { get; init; }
}

/// <summary>
/// Request body for retrofixing Linear TM history.
/// </summary>
public sealed record RetrofixLinearTmRequest
{
    public required decimal OriginalStartingTm { get; init; }
}
