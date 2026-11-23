using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.RemoveExercise;

[ChangesSyncArea(SyncArea.Workout)]
public sealed record RemoveExerciseCommand(Guid WorkoutId, Guid ExerciseId) : ICommand<Result<bool>>;
