using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Habits;

/// <summary>
/// A daily habit a person tracks: what it is, the full target and the minimum version that still
/// counts on a bad day ("a 30% day still counts"). Logs live in <see cref="HabitLog"/>, one per local date.
/// </summary>
public sealed class HabitDefinition : AggregateRoot<HabitId>
{
    private HabitDefinition(
        HabitId id,
        UserId userId,
        string name,
        HabitType type,
        decimal target,
        decimal minimum,
        string? unit,
        int order,
        string? icon,
        string? externalKey)
        : base(id)
    {
        UserId = userId;
        Name = name;
        Type = type;
        Target = target;
        Minimum = minimum;
        Unit = unit;
        Order = order;
        Icon = icon;
        ExternalKey = externalKey;
    }

    // EF Core constructor
    private HabitDefinition() { Name = string.Empty; }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }
    public HabitType Type { get; private set; }

    /// <summary>The full daily target (6 bottles, 3 walks, 1 for a checkbox).</summary>
    public decimal Target { get; private set; }

    /// <summary>The bad-day minimum that still keeps the streak alive. 0 &lt; Minimum &lt;= Target.</summary>
    public decimal Minimum { get; private set; }

    public string? Unit { get; private set; }
    public int Order { get; private set; }
    public bool Archived { get; private set; }
    public string? Icon { get; private set; }

    /// <summary>Stable key from an external source (e.g. <c>terran:walks</c>) so imports are idempotent.</summary>
    public string? ExternalKey { get; private set; }

    public static HabitDefinition Create(
        UserId userId,
        string name,
        HabitType type,
        decimal target,
        decimal minimum,
        string? unit,
        int order,
        string? icon = null,
        string? externalKey = null)
    {
        var cleanName = CleanName(name);
        var (cleanTarget, cleanMinimum) = NormaliseTargets(type, target, minimum);

        return new HabitDefinition(
            new HabitId(Guid.NewGuid()),
            userId,
            cleanName,
            type,
            cleanTarget,
            cleanMinimum,
            Clean(unit),
            order,
            Clean(icon),
            Clean(externalKey));
    }

    public void Rename(string name) => Name = CleanName(name);

    public void SetTargets(decimal target, decimal minimum)
    {
        (Target, Minimum) = NormaliseTargets(Type, target, minimum);
    }

    public void SetUnit(string? unit) => Unit = Clean(unit);

    public void SetIcon(string? icon) => Icon = Clean(icon);

    public void SetOrder(int order) => Order = order;

    public void Archive() => Archived = true;

    public void Unarchive() => Archived = false;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CleanName(string name)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Habit name cannot be empty.");
        var trimmed = name.Trim();
        CheckRule(trimmed.Length <= 100, "Habit name cannot be longer than 100 characters.");
        return trimmed;
    }

    private static (decimal Target, decimal Minimum) NormaliseTargets(HabitType type, decimal target, decimal minimum)
    {
        if (type == HabitType.Checkbox)
        {
            return (1, 1);
        }

        CheckRule(minimum > 0, "Habit minimum must be greater than 0.");
        CheckRule(minimum <= target, "Habit minimum cannot be greater than its target.");
        return (target, minimum);
    }
}
