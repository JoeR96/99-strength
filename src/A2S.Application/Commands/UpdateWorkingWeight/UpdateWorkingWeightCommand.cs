using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.UpdateWorkingWeight;

public sealed record UpdateWorkingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal NewWeight,
    WeightUnit Unit,
    string? Reason
) : ICommand<Result>;
