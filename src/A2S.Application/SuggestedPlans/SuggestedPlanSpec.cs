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
/// <see cref="Optional"/> inputs may be left empty: a training max left empty starts pending.
public sealed record SuggestedPlanInputSpec(string Key, string Label, string Unit, decimal? Default, decimal Minimum, decimal Maximum, bool Optional = false);

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
    string Label,
    string? WarmUp = null,
    FinisherSpec? Finisher = null,
    string? CardioTrack = null);

/// <summary>
/// A lift slot's finisher by name: a cardio track template key (<see cref="CardioTrackTemplates"/>) or a
/// fixed cool-down. Exactly one.
/// </summary>
public sealed record FinisherSpec(string? TrackKey = null, FixedActivity? Fixed = null);

/// <remarks>Without a kind, the applier picks one from the name (<see cref="A2S.Domain.Aggregates.Mobility.MobilityRoutine.DefaultKindFor"/>).</remarks>
public sealed record MobilityRoutineSpec(
    string Name,
    int TargetMinutes,
    IReadOnlyList<MobilityDrillDto> Drills,
    A2S.Domain.Aggregates.Mobility.MobilityRoutineKind? Kind = null);

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
    WeeklyAllowances Allowances,
    IReadOnlyList<MobilityRoutineSpec>? ExtraRoutines = null,
    IReadOnlyList<string>? CardioTracks = null,
    IReadOnlyList<int>? BlockSequence = null)
{
    /// <summary>Every routine the plan creates: the two named ones, then the extras (home routines).</summary>
    public IReadOnlyList<MobilityRoutineSpec> Routines => [MobilityA, MobilityB, .. ExtraRoutines ?? []];
}

/// <summary>Inputs for any template: values keyed by <see cref="SuggestedPlanInputSpec.Key"/> (null = left pending).</summary>
public sealed record PlanTemplateInputs(IReadOnlyDictionary<string, decimal?> Values, decimal StartWeightKg, DateOnly StartDate)
{
    public decimal? Get(string key) => Values.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// A starter plan anyone can preview and apply. Adding a template is adding a class that implements
/// this; <see cref="PlanTemplates"/> finds it. Everything it creates is the user's own editable data.
/// </summary>
public interface IPlanTemplate
{
    string Key { get; }
    string Name { get; }
    string Description { get; }
    decimal DefaultStartWeightKg { get; }
    IReadOnlyList<SuggestedPlanInputSpec> Inputs { get; }

    /// <summary>Pure: the plan as data for these inputs and start date (already a Monday).</summary>
    SuggestedPlanSpec Build(PlanTemplateInputs inputs);
}

/// <summary>Every <see cref="IPlanTemplate"/> in this assembly, in a stable order (oldest first).</summary>
public static class PlanTemplates
{
    public static IReadOnlyList<IPlanTemplate> All { get; } =
        typeof(IPlanTemplate).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPlanTemplate).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IPlanTemplate)Activator.CreateInstance(t)!)
            .OrderBy(t => t is CutTo85Template ? 0 : 1)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();

    public static IPlanTemplate? Find(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
