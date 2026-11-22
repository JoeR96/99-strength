using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>An exercise swap a phase suggests at its start ("Pause Squat" → "Squat (Barbell)"). Suggestion only: the person confirms it.</summary>
public sealed record PhaseSwap(string FromExercise, string ToExercise);

/// <summary>
/// A dated phase of the plan (Cut, Maintenance, Pause, Deload). Both ends inclusive. A phase can carry a
/// label ("Base", "Build", "Transition") and the changes it brings: free-text notes shown on Today the week
/// before it starts and on its first day, and exercise swaps offered at its start.
/// </summary>
public sealed record PlanPhase
{
    public const int MaxChanges = 10;
    public const int HeadsUpDays = 7;

    public PhaseType Type { get; }
    public DateOnly From { get; }
    public DateOnly To { get; }
    public string? Note { get; }
    public string? Label { get; }
    public IReadOnlyList<string> Changes { get; }
    public IReadOnlyList<PhaseSwap> Swaps { get; }

    public PlanPhase(
        PhaseType type,
        DateOnly from,
        DateOnly to,
        string? note,
        string? label = null,
        IReadOnlyList<string>? changes = null,
        IReadOnlyList<PhaseSwap>? swaps = null)
    {
        if (from > to)
        {
            throw new BusinessRuleViolationException("Phase start must be on or before its end");
        }

        var cleanChanges = (changes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        if (cleanChanges.Count > MaxChanges || cleanChanges.Any(c => c.Length > 200))
        {
            throw new BusinessRuleViolationException($"A phase holds at most {MaxChanges} changes of up to 200 characters");
        }

        var cleanSwaps = (swaps ?? []).ToList();
        if (cleanSwaps.Count > MaxChanges || cleanSwaps.Any(s => string.IsNullOrWhiteSpace(s.FromExercise) || string.IsNullOrWhiteSpace(s.ToExercise)))
        {
            throw new BusinessRuleViolationException("A phase swap needs the exercise to replace and its replacement");
        }

        Type = type;
        From = from;
        To = to;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        Changes = cleanChanges;
        Swaps = cleanSwaps.Select(s => new PhaseSwap(s.FromExercise.Trim(), s.ToExercise.Trim())).ToList();
    }

    /// <summary>Value equality, comparing the change and swap lists by content.</summary>
    public bool Equals(PlanPhase? other) =>
        other is not null && Type == other.Type && From == other.From && To == other.To && Note == other.Note && Label == other.Label
        && Changes.SequenceEqual(other.Changes) && Swaps.SequenceEqual(other.Swaps);

    public override int GetHashCode() => HashCode.Combine(Type, From, To, Note, Label, Changes.Count, Swaps.Count);

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public bool Overlaps(PlanPhase other) => From <= other.To && other.From <= To;

    /// <summary>True when the phase brings changes worth a heads-up.</summary>
    public bool HasChanges => Changes.Count > 0 || Swaps.Count > 0;

    /// <summary>The heads-up window: the week before the phase starts, through its first day.</summary>
    public bool InHeadsUpWindow(DateOnly date) => date >= From.AddDays(-HeadsUpDays) && date <= From;

    /// <summary>The same phase with new label, changes and swaps.</summary>
    public PlanPhase WithChanges(string? label, IReadOnlyList<string> changes, IReadOnlyList<PhaseSwap> swaps) =>
        new(Type, From, To, Note, label, changes, swaps);

    /// <summary>The phase with changes whose heads-up window holds <paramref name="date"/> (the soonest), or null.</summary>
    public static PlanPhase? HeadsUpOn(IEnumerable<PlanPhase> phases, DateOnly date) =>
        phases.Where(p => p.HasChanges && p.InHeadsUpWindow(date)).OrderBy(p => p.From).FirstOrDefault();
}
