using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.DeleteWorkout;

/// <summary>
/// Command to delete a workout program.
/// </summary>
[ChangesSyncArea(SyncArea.Workout)]
public sealed record DeleteWorkoutCommand(Guid WorkoutId) : ICommand<Result<bool>>;
