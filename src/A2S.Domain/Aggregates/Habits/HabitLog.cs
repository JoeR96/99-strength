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

    private static void CheckValue(decimal value) => CheckRule(value >= 0, "Habit value cannot be negative.");
}
