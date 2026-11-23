using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.UpdateExercises;

/// <summary>
/// Command to update one or more exercises in a workout.
/// </summary>
[ChangesSyncArea(SyncArea.Workout)]
public sealed record UpdateExercisesCommand(
    Guid WorkoutId,
    IReadOnlyList<ExerciseUpdateRequest> Updates
) : ICommand<Result<UpdateExercisesResult>>;
