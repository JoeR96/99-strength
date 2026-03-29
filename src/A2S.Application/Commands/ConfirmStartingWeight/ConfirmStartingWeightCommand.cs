using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.ConfirmStartingWeight;

public sealed record ConfirmStartingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal Weight,
    WeightUnit Unit
) : ICommand<Result>;
