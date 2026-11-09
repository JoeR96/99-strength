using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Habits;

/// <summary>
/// What was logged for one habit on one local date. Edits carry the client's timestamp so that
/// an offline device syncing later cannot overwrite a newer value (last write wins).
/// </summary>
public sealed class HabitLog : Entity<HabitLogId>
{
    private HabitLog(HabitLogId id, HabitId habitId, UserId userId, DateOnly date, decimal value, DateTimeOffset updatedAtUtc)
        : base(id)
    {
        HabitId = habitId;
        UserId = userId;
        Date = date;
        Value = value;
        UpdatedAtUtc = updatedAtUtc;
    }

    // EF Core constructor
    private HabitLog() { }

    public HabitId HabitId { get; private set; }
    public UserId UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Value { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static HabitLog Create(HabitId habitId, UserId userId, DateOnly date, decimal value, DateTimeOffset updatedAt)
    {
        CheckValue(value);
        return new HabitLog(new HabitLogId(Guid.NewGuid()), habitId, userId, date, value, updatedAt.ToUniversalTime());
    }

    /// <summary>
    /// The first tap of a day: the log starts at 0 and takes the tap exactly as <see cref="ApplyTap"/>
    /// would on an existing log, so a first <c>+3</c> is 3 and a first <c>-1</c> stays at 0.
    /// </summary>
    public static HabitLog CreateFromTap(HabitId habitId, UserId userId, DateOnly date, HabitType type, int delta, DateTimeOffset clientTimestamp)
    {
        return Create(habitId, userId, date, AfterTap(type, 0, delta), clientTimestamp);
    }

    /// <summary>
    /// The <c>+n</c> / <c>-n</c> tap. A counter moves by <paramref name="delta"/>, never below 0 and with no
    /// upper limit (7/6 is kept); a checkbox is ticked by a positive delta and unticked by anything else.
    /// Taps are applied in arrival order whatever their timestamp, because taps from two devices must both
    /// count, but the stored timestamp never moves backwards, so a late-arriving tap cannot reopen the door
    /// to an even older full-value write.
    /// The one tap that is not applied is a re-delivery of the write already stored
    /// (<see cref="IsRedeliveryOf"/>). Returns whether the tap was applied.
    /// </summary>
    public bool ApplyTap(HabitType type, int delta, DateTimeOffset clientTimestamp)
    {
        if (IsRedeliveryOf(clientTimestamp))
        {
            return false;
        }

        var at = clientTimestamp.ToUniversalTime();
        Set(AfterTap(type, Value, delta), at > UpdatedAtUtc ? at : UpdatedAtUtc);
        return true;
    }

    /// <summary>
    /// Whether a write stamped <paramref name="clientTimestamp"/> is the one this log already holds, sent
    /// again because its response never reached the client. A client stamps a write once and repeats that
    /// stamp on every retry, so an equal timestamp means the same write; any other timestamp, older or
    /// newer, is a different one.
    /// The comparison is in whole microseconds, which is what the <c>timestamptz</c> column keeps: a stamp
    /// with 100 ns ticks comes back from the database cut to the microsecond and must still match.
    /// A missing timestamp (the zero instant) never matches. Only the last write is remembered, so a
    /// re-delivery that arrives after another write has landed is not recognised.
    /// </summary>
    public bool IsRedeliveryOf(DateTimeOffset clientTimestamp)
    {
        return clientTimestamp != default
            && WholeMicroseconds(clientTimestamp) == WholeMicroseconds(UpdatedAtUtc);
    }

    /// <summary>Adds <paramref name="step"/>. Going past the target is allowed and reported as is (7/6).</summary>
    public void Increment(decimal step, DateTimeOffset at) => Set(Value + step, at);

    public void Increment(DateTimeOffset at) => Increment(1, at);

    /// <summary>Subtracts <paramref name="step"/>, never below 0.</summary>
    public void Decrement(decimal step, DateTimeOffset at) => Set(Math.Max(0, Value - step), at);

    public void Decrement(DateTimeOffset at) => Decrement(1, at);

    public void SetValue(decimal value, DateTimeOffset at)
    {
        CheckValue(value);
        Set(value, at);
    }

    /// <summary>0 to 1 and back, for checkbox habits.</summary>
    public void Toggle(DateTimeOffset at) => Set(Value == 0 ? 1 : 0, at);

    /// <summary>
    /// Last-write-wins: applies <paramref name="value"/> only when <paramref name="clientTimestamp"/> is not
    /// older than the stored one. Returns whether the value was applied.
    /// </summary>
    public bool Apply(decimal value, DateTimeOffset clientTimestamp)
    {
        CheckValue(value);
        if (clientTimestamp.ToUniversalTime() < UpdatedAtUtc)
        {
            return false;
        }

        Set(value, clientTimestamp);
        return true;
    }

    private void Set(decimal value, DateTimeOffset at)
    {
        Value = value;
        UpdatedAtUtc = at.ToUniversalTime();
    }

    private static long WholeMicroseconds(DateTimeOffset at) => at.UtcTicks / TimeSpan.TicksPerMicrosecond;

    private static decimal AfterTap(HabitType type, decimal current, int delta)
    {
        return type == HabitType.Checkbox
            ? (delta > 0 ? 1 : 0)
            : Math.Max(0, current + delta);
    }

    private static void CheckValue(decimal value) => CheckRule(value >= 0, "Habit value cannot be negative.");
}
