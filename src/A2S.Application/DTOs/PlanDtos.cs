using A2S.Application.SuggestedPlans;
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
    string Label,
    Guid? WarmUpRoutineId = null,
    Guid? FinisherCardioProtocolId = null,
    Guid? CardioTrackId = null,
    Guid? FinisherCardioTrackId = null,
    FixedActivityDto? FinisherFixed = null);

/// <summary>A fixed cool-down finisher: Kind "Walk" or "Treadmill", minutes, optional notes ("Easy incline").</summary>
public sealed record FixedActivityDto(string Kind, int Minutes, string? Notes);

public sealed record PlanPhaseDto(
    PhaseType Type,
    DateOnly From,
    DateOnly To,
    string? Note,
    string? Label = null,
    IReadOnlyList<string>? Changes = null,
    IReadOnlyList<PhaseSwapDto>? Swaps = null);

/// <summary>An exercise swap a phase suggests at its start (the person confirms it).</summary>
public sealed record PhaseSwapDto(string FromExercise, string ToExercise);

public sealed record CheckpointDto(DateOnly Date, decimal TargetKg, string? Note);

public sealed record WeeklyAllowancesDto(int FlexibleMeals, int AlcoholOccasions);

public sealed record CardioLogDto(
    int DurationSeconds,
    int? AvgHeartRate,
    int? Rpe,
    decimal? DistanceMeters,
    string? Notes,
    int? HighIntensityMinutes = null,
    EffortRating? Effort = null,
    IntensityUnit? IntensityUnit = null,
    decimal? IntensityValue = null,
    int? Floors = null,
    int? Steps = null,
    int? MaxHeartRate = null,
    int? Zone5Minutes = null,
    PainFlag Pain = PainFlag.None,
    string? PainArea = null,
    CardioLogSource Source = CardioLogSource.Manual);

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
    IReadOnlyList<bool>? DrillTicks,
    Guid? CardioTrackId = null);

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

/// <summary>
/// One session of a plan day. <c>HevyRoutineId</c> is the routine a push left in Hevy, or null when never pushed.
/// For a lift finished in gym mode, <c>GymSessionId</c> is the phone's client session id and <c>HevyPostStatus</c>
/// is Pending, Posted, Failed or Skipped (null when the session was not finished in gym mode).
/// </summary>
public sealed record TodaySessionDto(
    SessionKind Kind,
    string Label,
    SessionStatus Status,
    DayNumber? LiftDay,
    Guid? CardioProtocolId,
    Guid? MobilityRoutineId,
    Guid? CompletionId,
    string? HevyRoutineId = null,
    Guid? GymSessionId = null,
    string? HevyPostStatus = null,
    Guid? WarmUpRoutineId = null,
    Guid? FinisherCardioProtocolId = null,
    Guid? FinisherCardioTrackId = null,
    FixedActivityDto? FinisherFixed = null);

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

/// <summary>A soft schedule rule the plan breaks (e.g. Norwegian 4×4 the day before a lower-body lift).</summary>
public sealed record ScheduleWarningDto(string Code, string Message);

/// <summary>What <c>PUT /plans/{id}/schedule</c> returns: the plan and any soft-rule warnings. The schedule is saved either way.</summary>
public sealed record UpdateScheduleResult(TrainingPlanDto Plan, IReadOnlyList<ScheduleWarningDto> Warnings);

public sealed record SuggestedSessionPreviewDto(DayOfWeek Day, SessionKind Kind, string Label);

public sealed record SuggestedDayPreviewDto(string Label, IReadOnlyList<string> Exercises, int EstimatedMinutes, bool OverBudget);

/// <summary>A suggested plan as the onboarding card shows it, previewed for today.</summary>
public sealed record SuggestedPlanPreviewDto(
    string Key,
    string Name,
    string Description,
    DateOnly StartDate,
    IReadOnlyList<SuggestedPlanInputSpec> Inputs,
    IReadOnlyList<SuggestedSessionPreviewDto> Schedule,
    IReadOnlyList<SuggestedDayPreviewDto> LiftingDays,
    IReadOnlyList<string> Habits,
    IReadOnlyList<CheckpointDto> Checkpoints,
    bool AlreadyStarted);

/// <summary>A day from <c>GET /plans/upcoming</c>: the calendar day plus the planned sets of its lifts still to do.</summary>
public sealed record UpcomingDayDto(DateOnly Date, PhaseType? Phase, IReadOnlyList<TodaySessionDto> Sessions, IReadOnlyList<UpcomingLiftDto> Lifts);

/// <summary>A lift session's plan from the linked workout's current week. <c>Reps</c> of an AMRAP set is its rep-out target.</summary>
public sealed record UpcomingLiftDto(DayNumber LiftDay, string Label, int WeekNumber, bool IsDeloadWeek, IReadOnlyList<UpcomingExerciseDto> Exercises, UpcomingFinisherDto? Finisher);

public sealed record UpcomingExerciseDto(string Name, string Category, IReadOnlyList<UpcomingSetDto> Sets);

public sealed record UpcomingSetDto(decimal WeightKg, int Reps, bool IsAmrap);

public sealed record UpcomingFinisherDto(string Name, string Prescription);
