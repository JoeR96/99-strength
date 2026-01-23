using A2S.Application.Common;

namespace A2S.Application.Commands.SetActiveWorkout;

/// <summary>
/// Command to set a workout as the active program.
/// This will deactivate any currently active workout for the user.
/// </summary>
public sealed record SetActiveWorkoutCommand(Guid WorkoutId) : ICommand<Result<bool>>;
