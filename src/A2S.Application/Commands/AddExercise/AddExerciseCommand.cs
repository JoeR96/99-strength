using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;

namespace A2S.Application.Commands.AddExercise;

/// <summary>
/// Adds an exercise to a plan that is already running, at <see cref="CreateExerciseRequest.OrderInDay"/>
/// on its day; later exercises that day move down one. Same request shape as creating a workout, so a
/// RepsPerSet accessory can start from a known weight (e.g. what was just lifted) and progress from there.
/// </summary>
[ChangesSyncArea(SyncArea.Workout)]
public sealed record AddExerciseCommand(Guid WorkoutId, CreateExerciseRequest Exercise) : ICommand<Result<AddExerciseResult>>;

public sealed record AddExerciseResult(Guid ExerciseId, string Name, int AssignedDay, int OrderInDay);
