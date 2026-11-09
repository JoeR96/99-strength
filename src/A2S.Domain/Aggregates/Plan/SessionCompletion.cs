using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// A session that was done on a local date, recorded by hand or matched from a Hevy workout.
/// Child entity of <see cref="TrainingPlan"/>; created only through <see cref="TrainingPlan.CompleteSession"/>.
/// </summary>
public sealed class SessionCompletion : Entity<SessionCompletionId>
{
    private List<bool>? _drillTicks;

    internal SessionCompletion(
        SessionCompletionId id,
        DateOnly date,
        SessionKind kind,
        DayNumber? liftDay,
        CardioProtocolId? cardioProtocolId,
        MobilityRoutineId? mobilityRoutineId,
        CompletionSource source,
        string? hevyWorkoutId,
        DateTimeOffset recordedAtUtc,
        CardioLog? cardio,
        IReadOnlyList<bool>? drillTicks)
        : base(id)
    {
        CheckRule(kind != SessionKind.Rest, "A rest day is not completed");
        CheckRule(kind != SessionKind.Lift || liftDay is not null, "A lift completion needs the A2S2 day it trained");
        CheckRule(kind == SessionKind.Lift || liftDay is null, "Only lift completions carry a lift day");
        CheckRule(kind == SessionKind.Cardio || cardioProtocolId is null, "Only cardio completions carry a cardio protocol");
        CheckRule(kind == SessionKind.Cardio || cardio is null, "Only cardio completions carry a cardio log");
        CheckRule(kind == SessionKind.Mobility || mobilityRoutineId is null, "Only mobility completions carry a mobility routine");
        CheckRule(kind == SessionKind.Mobility || drillTicks is null, "Only mobility completions carry drill ticks");
        CheckRule(source != CompletionSource.Hevy || !string.IsNullOrWhiteSpace(hevyWorkoutId), "A Hevy completion needs the Hevy workout id");

        Date = date;
        Kind = kind;
        LiftDay = liftDay;
        CardioProtocolId = cardioProtocolId;
        MobilityRoutineId = mobilityRoutineId;
        Source = source;
        HevyWorkoutId = string.IsNullOrWhiteSpace(hevyWorkoutId) ? null : hevyWorkoutId.Trim();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        Cardio = cardio;
        _drillTicks = drillTicks?.ToList();
    }

    // EF Core constructor
    private SessionCompletion()
    {
    }

    public DateOnly Date { get; private set; }
    public SessionKind Kind { get; private set; }
    public DayNumber? LiftDay { get; private set; }
    public CardioProtocolId? CardioProtocolId { get; private set; }
    public MobilityRoutineId? MobilityRoutineId { get; private set; }
    public CompletionSource Source { get; private set; }
    public string? HevyWorkoutId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public CardioLog? Cardio { get; private set; }

    /// <summary>Optional per-drill ticks for a mobility completion, in routine order.</summary>
    public IReadOnlyList<bool>? DrillTicks => _drillTicks?.AsReadOnly();

    /// <summary>Whether this completion is for the same scheduled slot as the given one.</summary>
    internal bool MatchesSlot(DateOnly date, SessionKind kind, DayNumber? liftDay, CardioProtocolId? cardio, MobilityRoutineId? mobility) =>
        Date == date
        && Kind == kind
        && LiftDay == liftDay
        && CardioProtocolId == cardio
        && MobilityRoutineId == mobility;
}
