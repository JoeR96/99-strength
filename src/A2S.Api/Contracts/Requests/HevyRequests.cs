using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;

namespace A2S.Api.Contracts.Requests;

/// <summary>
/// Body for <c>POST /api/v1/hevy/inbox/{hevyWorkoutId}/assign</c>: which session the workout was.
/// <c>Extra</c> records the plan completion only (no A2S2 progression).
/// </summary>
public sealed record AssignHevyWorkoutRequest(DateOnly Date, SessionKind Kind, DayNumber? LiftDay = null, bool Extra = false);
