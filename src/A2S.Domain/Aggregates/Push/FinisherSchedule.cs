using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Push;

/// <summary>One phase-change notification of a guided finisher: when it is due and what it says.</summary>
public sealed record FinisherNudge(DateTimeOffset DueAtUtc, string Body, DateTimeOffset? SentAtUtc = null);

/// <summary>
/// The phase-change notifications for one live stair-finisher wizard (one gym session, keyed by the
/// client session id). The phone posts the whole schedule on Start and again on every pause, resume,
/// skip or ±30 s, which replaces it; finishing or abandoning deletes it. The finisher nudge loop sends
/// a due nudge once (it is marked sent before the save) and never sends one the phone has replaced.
/// </summary>
public sealed class FinisherSchedule : AggregateRoot<FinisherScheduleId>
{
    public const int MaxClientSessionIdLength = 64;
    public const int MaxNudges = 200;
    public const int MaxTextLength = 200;
    public const string DefaultTitle = "Stair finisher";

    /// <summary>How far ahead a schedule may reach (a finisher is minutes long; this is a sanity cap).</summary>
    public static readonly TimeSpan MaxHorizon = TimeSpan.FromHours(6);

    /// <summary>A nudge more than this late (the loop was down) is dropped rather than sent out of date.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    private readonly List<FinisherNudge> _nudges = new();

    private FinisherSchedule(FinisherScheduleId id, UserId userId, string clientSessionId)
        : base(id)
    {
        UserId = userId;
        ClientSessionId = clientSessionId;
        Title = DefaultTitle;
    }

    // EF Core constructor
    private FinisherSchedule()
    {
        ClientSessionId = string.Empty;
        Title = DefaultTitle;
    }

    public UserId UserId { get; private set; }
    public string ClientSessionId { get; private set; }

    /// <summary>The gym day the session belongs to; the notification deep-links to <c>/gym/{Day}?finisher=1</c>.</summary>
    public int Day { get; private set; }

    public string Title { get; private set; }

    /// <summary>Bumped on every replace.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The earliest unsent nudge (null: nothing left to send). Indexed for the loop's query.</summary>
    public DateTimeOffset? NextDueAtUtc { get; private set; }

    public IReadOnlyList<FinisherNudge> Nudges => _nudges.AsReadOnly();

    public string Url => $"/gym/{Day}?finisher=1";

    /// <summary>Collapses the session's notifications into one on the lock screen.</summary>
    public string Tag => $"finisher-{ClientSessionId}";

    public static FinisherSchedule Create(
        UserId userId,
        string clientSessionId,
        int day,
        string? title,
        IEnumerable<FinisherNudge> nudges,
        DateTimeOffset now)
    {
        var schedule = new FinisherSchedule(new FinisherScheduleId(Guid.NewGuid()), userId, CheckSessionId(clientSessionId));
        schedule.Replace(day, title, nudges, now);
        schedule.Revision = 1;
        return schedule;
    }

    /// <summary>Throws away every nudge (sent or not) and takes the new list; past ones are dropped.</summary>
    public void Replace(int day, string? title, IEnumerable<FinisherNudge> nudges, DateTimeOffset now)
    {
        CheckRule(day is >= 1 and <= 7, "A finisher schedule needs a day between 1 and 7.");
        var nowUtc = now.ToUniversalTime();
        var list = (nudges ?? throw new BusinessRuleViolationException("A finisher schedule needs its nudges.")).ToList();
        CheckRule(list.Count <= MaxNudges, $"A finisher schedule cannot have more than {MaxNudges} nudges.");

        var cleaned = new List<FinisherNudge>(list.Count);
        foreach (var nudge in list)
        {
            var body = (nudge.Body ?? string.Empty).Trim();
            CheckRule(body.Length > 0, "Every finisher nudge needs its text.");
            CheckRule(body.Length <= MaxTextLength, $"A finisher nudge cannot exceed {MaxTextLength} characters.");
            var due = nudge.DueAtUtc.ToUniversalTime();
            CheckRule(due <= nowUtc + MaxHorizon, "A finisher nudge cannot be more than 6 hours away.");
            if (due < nowUtc)
            {
                continue;
            }

            cleaned.Add(new FinisherNudge(due, body));
        }

        var cleanTitle = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
        CheckRule(cleanTitle.Length <= MaxTextLength, $"A finisher title cannot exceed {MaxTextLength} characters.");

        Day = day;
        Title = cleanTitle;
        _nudges.Clear();
        _nudges.AddRange(cleaned.OrderBy(n => n.DueAtUtc));
        Revision++;
        UpdatedAtUtc = nowUtc;
        RefreshNextDue();
    }

    /// <summary>
    /// Marks every unsent nudge that is due as sent and returns the one to deliver now: the latest
    /// due one that is not stale (older ones would only be replaced by it on the lock screen). Calling
    /// again at the same instant returns null, so a repeated tick never sends twice.
    /// </summary>
    public FinisherNudge? TakeDue(DateTimeOffset now)
    {
        var nowUtc = now.ToUniversalTime();
        FinisherNudge? toSend = null;
        for (var i = 0; i < _nudges.Count; i++)
        {
            var nudge = _nudges[i];
            if (nudge.SentAtUtc is not null || nudge.DueAtUtc > nowUtc)
            {
                continue;
            }

            _nudges[i] = nudge with { SentAtUtc = nowUtc };
            if (nowUtc - nudge.DueAtUtc <= StaleAfter)
            {
                toSend = _nudges[i];
            }
        }

        RefreshNextDue();
        return toSend;
    }

    /// <summary>Every nudge has gone out (or there were none): the schedule can be deleted.</summary>
    public bool IsExhausted => NextDueAtUtc is null;

    private void RefreshNextDue()
    {
        NextDueAtUtc = _nudges.Where(n => n.SentAtUtc is null).Select(n => (DateTimeOffset?)n.DueAtUtc).Min();
    }

    private static string CheckSessionId(string clientSessionId)
    {
        var trimmed = (clientSessionId ?? string.Empty).Trim();
        CheckRule(trimmed.Length > 0, "A finisher schedule needs the gym session id.");
        CheckRule(trimmed.Length <= MaxClientSessionIdLength, $"The session id cannot exceed {MaxClientSessionIdLength} characters.");
        CheckRule(trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "The session id may only use letters, digits, '-' and '_'.");
        return trimmed;
    }
}
