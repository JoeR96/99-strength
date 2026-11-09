using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Enums;

namespace A2S.Application.Queries.GetDayBudget;

/// <summary>
/// Weekly hard sets per muscle bucket, cap warnings and the session time estimate for one
/// lifting day. <paramref name="WeekNumber"/> defaults to the workout's current week.
/// </summary>
public sealed record GetDayBudgetQuery(Guid WorkoutId, DayNumber Day, int? WeekNumber = null)
    : IQuery<Result<DayBudgetDto>>;
