using A2S.Application.Common;

namespace A2S.Application.Commands.UpdateExercises;

/// <summary>
/// Command to update one or more exercises in a workout.
/// </summary>
public sealed record UpdateExercisesCommand(
    Guid WorkoutId,
    IReadOnlyList<ExerciseUpdateRequest> Updates
) : ICommand<Result<UpdateExercisesResult>>;
