using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.DTOs;

// Every DateOnly here serialises as yyyy-MM-dd (System.Text.Json default).

public sealed record ScheduledSessionDto(
    DayOfWeek Day,
    SessionKind Kind,
    DayNumber? LiftDay,
    Guid? CardioProtocolId,
    Guid? MobilityRoutineId,
    string Label);

public sealed record PlanPhaseDto(PhaseType Type, DateOnly From, DateOnly To, string? Note);

public sealed record CheckpointDto(DateOnly Date, decimal TargetKg, string? Note);

public sealed record WeeklyAllowancesDto(int FlexibleMeals, int AlcoholOccasions);

public sealed record CardioLogDto(int DurationSeconds, int? AvgHeartRate, int? Rpe, decimal? DistanceMeters, string? Notes, int? HighIntensityMinutes = null);

/// <summary>Weekly zone 4–5 minutes band.</summary>
public sealed record HighIntensityTargetDto(int MinMinutes, int MaxMinutes);

public sealed record SessionCompletionDto(
    Guid Id,
    DateOnly Date,
    SessionKind Kind,
    DayNumber? LiftDay,
    Guid? CardioProtocolId,
    Guid? MobilityRoutineId,
    CompletionSource Source,
    string? HevyWorkoutId,
    DateTimeOffset RecordedAtUtc,
    CardioLogDto? Cardio,
    IReadOnlyList<bool>? DrillTicks);

public sealed record AllowanceUseDto(DateOnly Date, AllowanceKind Kind);

public sealed record TrainingPlanDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    bool IsActive,
    Guid? LinkedWorkoutId,
    decimal StartWeightKg,
    WeeklyAllowancesDto Allowances,
    HighIntensityTargetDto HighIntensityTarget,
    IReadOnlyList<ScheduledSessionDto> Schedule,
    IReadOnlyList<PlanPhaseDto> Phases,
    IReadOnlyList<CheckpointDto> Checkpoints,
    IReadOnlyList<SessionCompletionDto> Completions,
    IReadOnlyList<AllowanceUseDto> AllowanceUses);

public sealed record TodaySessionDto(
    SessionKind Kind,
    string Label,
    SessionStatus Status,
    DayNumber? LiftDay,
    Guid? CardioProtocolId,
    Guid? MobilityRoutineId,
    Guid? CompletionId);

public sealed record WeeklyGoalsDto(
    DateOnly WeekStart,
    int LiftsPlanned,
    int LiftsDone,
    int CardioPlanned,
    int CardioDone,
    int MobilityPlanned,
    int MobilityDone,
    int FlexibleMealsUsed,
    int FlexibleMealsAllowed,
    int AlcoholOccasions,
    int AlcoholAllowed,
    int HighIntensityMinutesDone,
    int HighIntensityMinutesMin,
    int HighIntensityMinutesMax);

public sealed record TodayDto(DateOnly Date, PhaseType? Phase, IReadOnlyList<TodaySessionDto> Sessions, WeeklyGoalsDto Week);

public sealed record PlanDayDto(DateOnly Date, PhaseType? Phase, IReadOnlyList<TodaySessionDto> Sessions);

public sealed record CardioIntervalDto(string Label, int Seconds, CardioEffort Effort);

public sealed record CardioProtocolDto(
    Guid Id,
    string Name,
    CardioProtocolKind Kind,
    IReadOnlyList<CardioIntervalDto> Intervals,
    int TotalMinutes,
    string Notes,
    int? WeekIndex,
    bool RepeatWeek);

public sealed record DrillDoseDto(DoseUnit Unit, int Amount);

public sealed record MobilityDrillDto(
    string Name,
    int Sets,
    DrillDoseDto Dose,
    SideBias ExtraSetSide,
    string? Cues,
    string? StandingAlternative,
    bool PerSide);

public sealed record MobilityRoutineDto(Guid Id, string Name, int TargetMinutes, IReadOnlyList<MobilityDrillDto> Drills);
