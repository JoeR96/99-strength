namespace A2S.Domain.Aggregates.Push;

/// <summary>The nudges the app sends; each goes out at most once per user per local day.</summary>
public enum PushNudgeKind
{
    /// <summary>"Time to train — Upper A" at the session's scheduled start.</summary>
    SessionStart = 0,

    /// <summary>"Still under minimum: Water 2/4" at 20:00 local.</summary>
    HabitsUnderMinimum = 1
}
