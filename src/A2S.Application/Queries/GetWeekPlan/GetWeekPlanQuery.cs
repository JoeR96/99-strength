using A2S.Application.Common;

namespace A2S.Application.Queries.GetWeekPlan;

/// <summary>
/// Query to get the planned sets for a specific week and day.
/// </summary>
public sealed record GetWeekPlanQuery(
    Guid? WorkoutId,
    int WeekNumber,
    int DayNumber) : IQuery<Result<WeekPlanDto>>;
