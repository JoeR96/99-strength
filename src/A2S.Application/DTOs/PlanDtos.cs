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
    CardioLogSource Source = CardioLogSource.Manual,
    decimal? WorkMinutes = null);

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
    Guid? CardioTrackId = null,
    int? DurationMinutes = null);

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
    bool PerSide,
    IReadOnlyList<DrillLevelDto>? Levels = null,
    int CurrentLevel = 0,
    int CleanStreak = 0,
    int? SuggestedLevel = null);

/// <summary>A dose range a level progresses through (hold 15 → 30 s, step 5 s).</summary>
public sealed record DoseRangeDto(int Min, int Max, int Step);

/// <summary>One rung of a drill's progression chain.</summary>
public sealed record DrillLevelDto(string Name, string? Description, int Sets, DrillDoseDto Dose, DoseRangeDto? Range, string? Cues);

/// <summary>One audited progression change.</summary>
public sealed record DrillProgressionEntryDto(Guid ChangeId, DateTimeOffset At, string DrillName, DrillChangeKind Kind, string Reason, bool Undone);

public sealed record MobilityRoutineDto(
    Guid Id,
    string Name,
    int TargetMinutes,
    IReadOnlyList<MobilityDrillDto> Drills,
    MobilityRoutineKind Kind = MobilityRoutineKind.Stretch,
    string? FollowAlongUrl = null,
    IReadOnlyList<DrillProgressionEntryDto>? RecentChanges = null,
    bool CanUndo = false);

/// <summary>What recording a session's drill feedback returns: the routine now, and the changes it made.</summary>
public sealed record DrillFeedbackResultDto(MobilityRoutineDto Routine, IReadOnlyList<DrillProgressionEntryDto> Changes);

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
    bool AlreadyStarted)
{
    public IReadOnlyList<PlanPhaseDto> Phases { get; init; } = [];
    public IReadOnlyList<TemplateSessionPreviewDto> Sessions { get; init; } = [];
    public IReadOnlyList<TemplateSlotPreviewDto> Slots { get; init; } = [];
    public IReadOnlyList<TemplateRoutinePreviewDto> Routines { get; init; } = [];
    public IReadOnlyList<TemplateTrackPreviewDto> CardioTracks { get; init; } = [];

    /// <summary>What is left to fill in after applying: pending training maxes, starting weights.</summary>
    public IReadOnlyList<string> ToFillIn { get; init; } = [];
    public IReadOnlyList<int> BlockSequence { get; init; } = [1, 2, 3];
    public int ProgramWeeks { get; init; } = 21;
}

/// <summary>One gym session of a template: "2a Bent Over Row (Barbell) 3 × 8–10, reps per set".</summary>
public sealed record TemplateSessionPreviewDto(
    string Label, DayNumber LiftDay, string? WarmUp, string? Finisher, int EstimatedMinutes, IReadOnlyList<TemplateExercisePreviewDto> Exercises);

public sealed record TemplateExercisePreviewDto(string Slot, string Name, string Prescription, string Progression, string? Note);

public sealed record TemplateSlotPreviewDto(
    DayOfWeek Day, SessionKind Kind, string Label, string? WarmUp, string? Finisher, string? Routine, string? Track);

public sealed record TemplateRoutinePreviewDto(string Name, int TargetMinutes, IReadOnlyList<TemplateDrillPreviewDto> Drills);

public sealed record TemplateDrillPreviewDto(string Name, string Dose, string? Cues);

public sealed record TemplateTrackPreviewDto(string Key, string Name, string Description);

/// <summary>A day from <c>GET /plans/upcoming</c>: the calendar day plus the planned sets of its lifts still to do.</summary>
public sealed record UpcomingDayDto(DateOnly Date, PhaseType? Phase, IReadOnlyList<TodaySessionDto> Sessions, IReadOnlyList<UpcomingLiftDto> Lifts);

/// <summary>A lift session's plan from the linked workout's current week. <c>Reps</c> of an AMRAP set is its rep-out target.</summary>
public sealed record UpcomingLiftDto(DayNumber LiftDay, string Label, int WeekNumber, bool IsDeloadWeek, IReadOnlyList<UpcomingExerciseDto> Exercises, UpcomingFinisherDto? Finisher)
{
    /// <summary>The finisher as a planned item (prescription, progression type, sessions to step up). Additive; <see cref="Finisher"/> stays.</summary>
    public PlannedCardioDto? Cardio { get; init; }
}

/// <summary>
/// <see cref="IsTrainingMaxPending"/>: an A2S2 lift with no training max yet; its sets weigh 0, so show reps and
/// <see cref="IntensityPercentage"/> (0.70 = 70 %) and "Set your training max" instead of kg.
/// <see cref="Cardio"/> (additive): a cardio exercise's whole session, e.g. "3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)";
/// its <see cref="Sets"/> is empty. Null for lifts. Terran CC reads this record: never rename or retype a field.
/// </summary>
public sealed record UpcomingExerciseDto(string Name, string Category, IReadOnlyList<UpcomingSetDto> Sets, bool IsTrainingMaxPending = false, decimal? IntensityPercentage = null, string? Cardio = null);

public sealed record UpcomingSetDto(decimal WeightKg, int Reps, bool IsAmrap);

public sealed record UpcomingFinisherDto(string Name, string Prescription);
