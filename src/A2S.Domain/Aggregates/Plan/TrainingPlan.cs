using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// The Whole Plan beside the A2S2 <c>Workout</c>: a weekly schedule template, dated phases,
/// trend-weight checkpoints, weekly allowances and the sessions actually done. It reads the linked
/// workout's history through the calendar service; it never writes the workout.
/// </summary>
public sealed class TrainingPlan : AggregateRoot<TrainingPlanId>
{
    public const decimal MinimumStartWeightKg = 30m;
    public const decimal MaximumStartWeightKg = 300m;

    private readonly List<ScheduledSession> _schedule = new();
    private readonly List<PlanPhase> _phases = new();
    private readonly List<Checkpoint> _checkpoints = new();
    private readonly List<SessionCompletion> _completions = new();
    private readonly List<AllowanceUse> _allowanceUses = new();

    private TrainingPlan(
        TrainingPlanId id,
        UserId userId,
        string name,
        DateOnly startDate,
        List<ScheduledSession> schedule,
        decimal startWeightKg,
        WeeklyAllowances allowances)
        : base(id)
    {
        UserId = userId;
        Name = name;
        StartDate = startDate;
        IsActive = true;
        _schedule = schedule;
        StartWeightKg = startWeightKg;
        Allowances = allowances;
        HighIntensityTarget = HighIntensityTarget.Default;
    }

    // EF Core constructor
    private TrainingPlan()
    {
        Name = string.Empty;
        Allowances = WeeklyAllowances.Default;
        HighIntensityTarget = HighIntensityTarget.Default;
    }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }

    /// <summary>Always a Monday: the coming Monday from the requested start.</summary>
    public DateOnly StartDate { get; private set; }

    public bool IsActive { get; private set; }
    public WorkoutId? LinkedWorkoutId { get; private set; }
    public decimal StartWeightKg { get; private set; }
    public WeeklyAllowances Allowances { get; private set; }

    /// <summary>Weekly zone 4–5 minutes band (default 15–30).</summary>
    public HighIntensityTarget HighIntensityTarget { get; private set; }

    /// <summary>Ordered Monday-first, then by insertion within a day.</summary>
    public IReadOnlyList<ScheduledSession> Schedule => _schedule.AsReadOnly();

    /// <summary>Non-overlapping, ordered by start date.</summary>
    public IReadOnlyList<PlanPhase> Phases => _phases.AsReadOnly();

    /// <summary>Unique dates, ordered by date.</summary>
    public IReadOnlyList<Checkpoint> Checkpoints => _checkpoints.AsReadOnly();

    public IReadOnlyList<SessionCompletion> Completions => _completions.AsReadOnly();
    public IReadOnlyList<AllowanceUse> AllowanceUses => _allowanceUses.AsReadOnly();

    public static TrainingPlan Create(
        UserId userId,
        string name,
        DateOnly requestedStart,
        IEnumerable<ScheduledSession> schedule,
        decimal startWeightKg,
        WeeklyAllowances? allowances = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Plan name is required");
        CheckRule(
            startWeightKg >= MinimumStartWeightKg && startWeightKg <= MaximumStartWeightKg,
            $"Start weight must be between {MinimumStartWeightKg} and {MaximumStartWeightKg} kg");

        var plan = new TrainingPlan(
            new TrainingPlanId(Guid.NewGuid()),
            userId,
            name.Trim(),
            LocalDates.ComingMonday(requestedStart),
            OrderedSchedule(schedule),
            startWeightKg,
            allowances ?? WeeklyAllowances.Default);

        plan.AddDomainEvent(new TrainingPlanStarted(plan.Id, userId, plan.StartDate));
        return plan;
    }

    public void ReplaceSchedule(IEnumerable<ScheduledSession> schedule)
    {
        var ordered = OrderedSchedule(schedule);
        _schedule.Clear();
        _schedule.AddRange(ordered);
    }

    public void AddPhase(PlanPhase phase)
    {
        CheckRule(phase is not null, "Phase is required");
        CheckRule(!_phases.Any(p => p.Overlaps(phase!)), "Phases may not overlap");

        _phases.Add(phase!);
        _phases.Sort((a, b) => a.From.CompareTo(b.From));
        AddDomainEvent(new PhaseAdded(Id, phase!.Type, phase.From, phase.To));
    }

    /// <summary>Removes the phase starting on <paramref name="from"/>. False when there is none.</summary>
    public bool RemovePhase(DateOnly from) => _phases.RemoveAll(p => p.From == from) > 0;

    public void AddCheckpoint(Checkpoint checkpoint)
    {
        CheckRule(checkpoint is not null, "Checkpoint is required");
        CheckRule(_checkpoints.All(c => c.Date != checkpoint!.Date), "Checkpoint dates must be unique");

        _checkpoints.Add(checkpoint!);
        _checkpoints.Sort((a, b) => a.Date.CompareTo(b.Date));
    }

    public bool RemoveCheckpoint(DateOnly date) => _checkpoints.RemoveAll(c => c.Date == date) > 0;

    public void SetHighIntensityTarget(HighIntensityTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        HighIntensityTarget = target;
    }

    public void LinkWorkout(WorkoutId workoutId) => LinkedWorkoutId = workoutId;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>The scheduled sessions for a date; empty before the plan starts.</summary>
    public IReadOnlyList<ScheduledSession> SessionsOn(DateOnly date)
    {
        if (date < StartDate)
        {
            return Array.Empty<ScheduledSession>();
        }

        return _schedule.Where(s => s.Day == date.DayOfWeek).ToList();
    }

    public PlanPhase? PhaseOn(DateOnly date) => _phases.FirstOrDefault(p => p.Contains(date));

    public bool IsPaused(DateOnly date) => PhaseOn(date)?.Type == PhaseType.Pause;

    /// <summary>
    /// Records a session as done. Idempotent: the same Hevy workout id, or a manual completion for a
    /// slot already completed, returns the existing completion without adding another.
    /// </summary>
    public SessionCompletion CompleteSession(
        DateOnly date,
        SessionKind kind,
        CompletionSource source,
        DayNumber? liftDay = null,
        CardioProtocolId? cardio = null,
        MobilityRoutineId? mobility = null,
        string? hevyWorkoutId = null,
        CardioLog? log = null,
        IReadOnlyList<bool>? drillTicks = null,
        DateTimeOffset? recordedAt = null)
    {
        if (!string.IsNullOrWhiteSpace(hevyWorkoutId))
        {
            var byHevyId = _completions.FirstOrDefault(c =>
                string.Equals(c.HevyWorkoutId, hevyWorkoutId.Trim(), StringComparison.Ordinal));
            if (byHevyId is not null)
            {
                return byHevyId;
            }
        }

        if (source == CompletionSource.Manual)
        {
            var sameSlot = _completions.FirstOrDefault(c => c.MatchesSlot(date, kind, liftDay, cardio, mobility));
            if (sameSlot is not null)
            {
                return sameSlot;
            }
        }

        var completion = new SessionCompletion(
            new SessionCompletionId(Guid.NewGuid()),
            date,
            kind,
            liftDay,
            cardio,
            mobility,
            source,
            hevyWorkoutId,
            recordedAt ?? DateTimeOffset.UtcNow,
            log,
            drillTicks);

        _completions.Add(completion);
        AddDomainEvent(new SessionCompleted(Id, completion.Id, date, kind, source, completion.RecordedAtUtc));
        return completion;
    }

    public bool UndoCompletion(SessionCompletionId completionId) =>
        _completions.RemoveAll(c => c.Id == completionId) > 0;

    /// <summary>Spends an allowance on a date. Going over the weekly allowance is allowed; goals report the overflow.</summary>
    public AllowanceUse UseAllowance(DateOnly date, AllowanceKind kind)
    {
        var use = new AllowanceUse(date, kind);
        _allowanceUses.Add(use);
        return use;
    }

    /// <summary>Removes one matching allowance use. False when there is none.</summary>
    public bool RemoveAllowanceUse(DateOnly date, AllowanceKind kind)
    {
        var index = _allowanceUses.FindIndex(u => u.Date == date && u.Kind == kind);
        if (index < 0)
        {
            return false;
        }

        _allowanceUses.RemoveAt(index);
        return true;
    }

    private static List<ScheduledSession> OrderedSchedule(IEnumerable<ScheduledSession> schedule)
    {
        var list = schedule?.ToList() ?? new List<ScheduledSession>();
        CheckRule(list.Any(s => s.Kind != SessionKind.Rest), "A plan needs at least one non-rest session");

        return list.OrderBy(s => s.DayOrder).ToList();
    }
}
