using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

public enum FixedActivityKind
{
    Walk = 0,
    Treadmill = 1
}

/// <summary>
/// A simple, fixed piece of cardio done after the lifts, e.g. "Treadmill, easy incline, 10 min". Not a
/// protocol (no LISS 30–40 min rule) and not a track (nothing is evaluated): it is ticked off and logged.
/// </summary>
public sealed record FixedActivity
{
    public const int MinimumMinutes = 1;
    public const int MaximumMinutes = 60;
    public const int MaximumNotesLength = 100;

    public FixedActivityKind Kind { get; }
    public int Minutes { get; }
    public string? Notes { get; }

    public FixedActivity(FixedActivityKind kind, int minutes, string? notes = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new BusinessRuleViolationException("Unknown cool-down kind");
        }

        if (minutes is < MinimumMinutes or > MaximumMinutes)
        {
            throw new BusinessRuleViolationException($"A cool-down is {MinimumMinutes} to {MaximumMinutes} minutes");
        }

        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > MaximumNotesLength })
        {
            throw new BusinessRuleViolationException($"Cool-down notes are at most {MaximumNotesLength} characters");
        }

        Kind = kind;
        Minutes = minutes;
        Notes = trimmed;
    }

    /// <summary>"Treadmill" or "Walk", the name shown in gym mode and Today.</summary>
    public string DisplayName() => Kind == FixedActivityKind.Treadmill ? "Treadmill" : "Walk";

    /// <summary>"10 min · Easy incline".</summary>
    public string Describe() => Notes is null ? $"{Minutes} min" : $"{Minutes} min · {Notes}";

    public static FixedActivity EasyInclineTreadmill(int minutes = 10) => new(FixedActivityKind.Treadmill, minutes, "Easy incline");
}
