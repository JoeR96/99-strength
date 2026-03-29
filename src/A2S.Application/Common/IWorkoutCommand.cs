namespace A2S.Application.Common;

/// <summary>
/// Marker interface for commands that operate on a specific workout.
/// Used by AuthorizedWorkoutBehavior to automatically load and authorize the workout.
/// </summary>
public interface IWorkoutCommand<out TResponse> : ICommand<TResponse>
{
    Guid WorkoutId { get; }
}
