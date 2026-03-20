using A2S.Application.Common;

namespace A2S.Application.Commands.RemoveExercise;

public sealed record RemoveExerciseCommand(Guid WorkoutId, Guid ExerciseId) : ICommand<Result<bool>>;
