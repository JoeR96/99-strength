using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Push;

/// <summary>
/// Proof that a nudge went out: one row per user, local date and kind, so the minute-by-minute
/// nudge service never sends the same reminder twice in a day.
/// </summary>
public sealed class PushNudgeLog : AggregateRoot<PushNudgeLogId>
{
    private PushNudgeLog(PushNudgeLogId id, UserId userId, DateOnly localDate, PushNudgeKind kind, DateTimeOffset sentAtUtc)
        : base(id)
    {
        UserId = userId;
        LocalDate = localDate;
        Kind = kind;
        SentAtUtc = sentAtUtc;
    }

    // EF Core constructor
    private PushNudgeLog() { }

    public UserId UserId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public PushNudgeKind Kind { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    public static PushNudgeLog Create(UserId userId, DateOnly localDate, PushNudgeKind kind, DateTimeOffset sentAt) =>
        new(new PushNudgeLogId(Guid.NewGuid()), userId, localDate, kind, sentAt.ToUniversalTime());
}
