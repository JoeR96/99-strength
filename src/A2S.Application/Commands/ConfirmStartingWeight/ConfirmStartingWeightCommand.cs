using A2S.Application.Common;
using A2S.Domain.Enums;
using MediatR;

namespace A2S.Application.Commands.ConfirmStartingWeight;

public sealed record ConfirmStartingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal Weight,
    WeightUnit Unit
) : IRequest<Result>;
