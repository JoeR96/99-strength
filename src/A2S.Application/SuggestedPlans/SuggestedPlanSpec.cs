using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.SuggestedPlans;

/// <summary>The only things a person types to start a suggested plan: four training maxes and a start weight.</summary>
public sealed record SuggestedPlanInputs(
    decimal BenchTm,
    decimal SquatTm,
    decimal OhpTm,
    decimal DeadliftTm,
    decimal StartWeightKg,
    DateOnly Today);

/// <summary>Describes one input the onboarding card asks for.</summary>
public sealed record SuggestedPlanInputSpec(string Key, string Label, string Unit, decimal? Default, decimal Minimum, decimal Maximum);

/// <summary>
/// One slot of the weekly template before the cardio protocols and mobility routines exist.
/// <see cref="Cardio"/> names the protocol kind the slot points at (null = open slot, "C25K or LISS");
/// <see cref="MobilityRoutine"/> names the routine by its spec name.
/// </summary>
public sealed record ScheduledSessionSpec(
    DayOfWeek Day,
    SessionKind Kind,
    DayNumber? LiftDay,
    CardioProtocolKind? Cardio,
    string? MobilityRoutine,
    string Label);

public sealed record MobilityRoutineSpec(string Name, int TargetMinutes, IReadOnlyList<MobilityDrillDto> Drills);

public sealed record CardioProtocolSpec(CardioProtocolKind Kind, int? WeekIndex, int? Minutes, string? Note);

public sealed record HabitSpec(
    string Name,
    HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon,
    string ExternalKey);

/// <summary>Two exercises done back to back on a lifting day, named by template.</summary>
public sealed record SupersetSpec(DayNumber Day, string First, string Second);

/// <summary>A lifting day with its time estimate (real <see cref="SessionTimeEstimator"/>, <see cref="RestDefaults"/>).</summary>
public sealed record LiftingDaySpec(DayNumber Day, string Label, SessionTimeEstimate Estimate);

/// <summary>
/// Everything a suggested plan is, as data. <see cref="StartSuggestedPlanCommand"/> turns it into
/// aggregates; nothing here is special-cased afterwards, so the plan is editable like any other.
/// Side notes ("start left", "pause the last rep") live in <see cref="ExerciseNotes"/> keyed by
/// template name because <c>Exercise</c> has no side or note field; supersets live in
/// <see cref="Supersets"/> because <c>Workout</c> has no superset concept.
/// </summary>
public sealed record SuggestedPlanSpec(
    string Key,
    string Name,
    string Description,
    DateOnly StartDate,
    decimal StartWeightKg,
    ProgramVariant Variant,
    IReadOnlyList<CreateExerciseRequest> Exercises,
    IReadOnlyDictionary<string, string> ExerciseNotes,
    IReadOnlyList<SupersetSpec> Supersets,
    IReadOnlyList<LiftingDaySpec> LiftingDays,
    IReadOnlyList<ScheduledSessionSpec> Schedule,
    MobilityRoutineSpec MobilityA,
    MobilityRoutineSpec MobilityB,
    IReadOnlyList<CardioProtocolSpec> Cardio,
    IReadOnlyList<HabitSpec> Habits,
    IReadOnlyList<PlanPhaseDto> Phases,
    IReadOnlyList<CheckpointDto> Checkpoints,
    VolumeCaps Caps,
    WeeklyAllowances Allowances);
