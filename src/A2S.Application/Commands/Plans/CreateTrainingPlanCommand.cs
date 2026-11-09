using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Commands.Plans;

/// <summary>
/// Creates a training plan for the current user and makes it the active one. The start snaps to the
/// coming Monday. Optionally links the A2S2 workout whose completed days count as lifts.
/// </summary>
public sealed record CreateTrainingPlanCommand(
    string Name,
    DateOnly RequestedStart,
    IReadOnlyList<ScheduledSessionDto> Schedule,
    decimal StartWeightKg,
    int FlexibleMeals = 1,
    int AlcoholOccasions = 1,
    Guid? LinkedWorkoutId = null) : ICommand<Result<TrainingPlanDto>>;
