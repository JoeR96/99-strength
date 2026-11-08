using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;

namespace A2S.Api.Contracts.Requests;

/// <summary>Body for <c>POST /api/v1/plans/suggested/cut-to-85</c>. Training maxes in kg; start weight defaults to 95 kg.</summary>
public sealed record StartSuggestedPlanRequest(decimal BenchTm, decimal SquatTm, decimal OhpTm, decimal DeadliftTm, decimal? StartWeightKg = null);

/// <summary>Body for <c>PUT /api/v1/plans/{id}/schedule</c>.</summary>
public sealed record UpdateScheduleRequest(IReadOnlyList<ScheduledSessionDto> Schedule);

/// <summary>Body for <c>POST /api/v1/plans/{id}/phases</c>.</summary>
public sealed record AddPhaseRequest(PhaseType Type, DateOnly From, DateOnly To, string? Note);

/// <summary>Body for <c>POST /api/v1/plans/{id}/checkpoints</c>.</summary>
public sealed record AddCheckpointRequest(DateOnly Date, decimal TargetKg, string? Note);

/// <summary>Body for <c>POST /api/v1/plans/{id}/link-workout</c>.</summary>
public sealed record LinkWorkoutRequest(Guid WorkoutId);

/// <summary>Body for <c>POST /api/v1/plans/{id}/completions</c>.</summary>
public sealed record CompleteSessionRequest(
    DateOnly Date,
    SessionKind Kind,
    DayNumber? LiftDay = null,
    Guid? CardioProtocolId = null,
    Guid? MobilityRoutineId = null,
    CardioLogDto? Cardio = null,
    IReadOnlyList<bool>? DrillTicks = null,
    string? HevyWorkoutId = null);

/// <summary>Body for <c>POST /api/v1/plans/{id}/allowances</c>.</summary>
public sealed record UseAllowanceRequest(DateOnly Date, AllowanceKind Kind);

/// <summary>Body for <c>PUT /api/v1/cardio-protocols/{id}/repeat-week</c>.</summary>
public sealed record SetRepeatWeekRequest(bool Repeat);

/// <summary>Body for <c>PUT /api/v1/mobility-routines/{id}/drills</c>.</summary>
public sealed record ReplaceDrillsRequest(IReadOnlyList<MobilityDrillDto> Drills);
