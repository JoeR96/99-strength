using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.SubstituteExercise;

/// <summary>
/// Command to permanently substitute an exercise with another.
/// Can optionally change the progression type and configuration.
/// </summary>
[ChangesSyncArea(SyncArea.Workout)]
public sealed record SubstituteExerciseCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    string NewExerciseName,
    string? NewExternalTemplateId = null,
    string? Reason = null,
    ProgressionConfigDto? NewProgressionConfig = null
) : ICommand<Result<SubstituteExerciseResult>>;
