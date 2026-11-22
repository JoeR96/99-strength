namespace A2S.Domain.Aggregates.Mobility;

/// <summary>How a drill felt in a session. Clean = cues held without strain.</summary>
public enum DrillOutcome
{
    Clean,
    Strained,
    Stopped,
    Skipped
}

/// <summary>What changed on a drill, for the audit trail.</summary>
public enum DrillChangeKind
{
    DoseIncreased,
    LevelUpSuggested,
    LevelUp,
    SuggestionDeclined,
    SteppedBack,
    Held,
    HoldIncreased,
    SessionRecorded
}

/// <summary>One drill's feedback from a finished session.</summary>
public sealed record DrillFeedback(string DrillName, DrillOutcome Outcome);

/// <summary>The result of evaluating one drill: the new state and what changed (null when only the count moved).</summary>
public sealed record DrillEvaluation(MobilityDrill Drill, DrillChangeKind? Change, string? Reason);

/// <summary>
/// One audit entry. Entries made by the same action share a <see cref="ChangeId"/> so undo reverts them together;
/// <see cref="Before"/> is the drill as it was, which undo restores.
/// </summary>
public sealed record DrillProgressionEntry(
    Guid ChangeId,
    DateTimeOffset At,
    string? SessionKey,
    string DrillName,
    DrillChangeKind Kind,
    string Reason,
    MobilityDrill? Before,
    bool Undone = false);

/// <summary>
/// The light mobility progression rule (brief § 5c). After <c>N</c> (default 2) clean sessions in a row: inside the
/// level's dose range add one step; at the top (or with no range) suggest the next level, which the user accepts or
/// declines. A strain or a stop resets the count and steps the dose back once (or holds at the bottom) and withdraws
/// any pending suggestion. Drills without levels are not progressed. Pure: no I/O, no clock.
/// </summary>
public static class DrillProgressionRule
{
    public const int DefaultCleanSessions = 2;

    public static DrillEvaluation Evaluate(MobilityDrill drill, DrillOutcome outcome, int requiredCleanSessions = DefaultCleanSessions)
    {
        if (drill.CurrentLevelDetails is not { } level || outcome == DrillOutcome.Skipped)
        {
            return new DrillEvaluation(drill, null, null);
        }

        var unit = drill.Dose.Unit;
        if (outcome is DrillOutcome.Strained or DrillOutcome.Stopped)
        {
            var what = outcome == DrillOutcome.Strained ? "Strained" : "Stopped early";
            if (level.Range is { } r && drill.Dose.Amount > r.Min)
            {
                var back = new DrillDose(unit, Math.Max(r.Min, drill.Dose.Amount - r.Step));
                return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, back, 0, null), DrillChangeKind.SteppedBack,
                    $"{what}: back from {Format(drill.Dose)} to {Format(back)}");
            }

            return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, drill.Dose, 0, null), DrillChangeKind.Held,
                $"{what}: holding at {Format(drill.Dose)}");
        }

        var streak = drill.CleanStreak + 1;
        if (streak < Math.Max(1, requiredCleanSessions))
        {
            return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, drill.Dose, streak, drill.SuggestedLevel), null, null);
        }

        var clean = $"{streak} clean sessions";
        if (level.Range is { } range && drill.Dose.Amount < range.Max)
        {
            var up = new DrillDose(unit, Math.Min(range.Max, drill.Dose.Amount + range.Step));
            return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, up, 0, null), DrillChangeKind.DoseIncreased,
                $"{clean}: {Format(drill.Dose)} to {Format(up)}");
        }

        var next = drill.CurrentLevel + 1;
        if (next < drill.Levels.Count)
        {
            return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, drill.Dose, 0, next), DrillChangeKind.LevelUpSuggested,
                $"{clean} at the top: ready for {drill.Levels[next].Label}");
        }

        return new DrillEvaluation(drill.WithProgress(drill.CurrentLevel, drill.Dose, 0, null), null, null);
    }

    public static string Format(DrillDose dose) => dose.Unit switch
    {
        DoseUnit.Seconds => $"{dose.Amount} s",
        DoseUnit.Breaths => $"{dose.Amount} breaths",
        _ => $"{dose.Amount} reps"
    };
}
