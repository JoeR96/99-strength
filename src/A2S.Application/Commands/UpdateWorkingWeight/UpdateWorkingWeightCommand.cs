using A2S.Application.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.UpdateWorkingWeight;

public sealed record UpdateWorkingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal NewWeight,
    WeightUnit Unit,
    string? Reason
) : IRequest<Result>;
