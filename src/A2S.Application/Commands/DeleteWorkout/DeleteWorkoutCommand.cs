using A2S.Application.Common;

namespace A2S.Application.Commands.DeleteWorkout;

/// <summary>
/// Command to delete a workout program.
/// </summary>
public sealed record DeleteWorkoutCommand(Guid WorkoutId) : ICommand<Result<bool>>;
