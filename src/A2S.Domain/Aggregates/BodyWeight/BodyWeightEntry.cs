using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.BodyWeight;

/// <summary>
/// One morning weigh-in: a user, a local date and a weight in kilograms. One entry per user per date.
/// </summary>
public sealed class BodyWeightEntry : AggregateRoot<BodyWeightEntryId>
{
    public const decimal MinKg = 30m;
    public const decimal MaxKg = 300m;

    private BodyWeightEntry(BodyWeightEntryId id, UserId userId, DateOnly date, decimal kg, DateTimeOffset recordedAtUtc)
        : base(id)
    {
        UserId = userId;
        Date = date;
        Kg = kg;
        RecordedAtUtc = recordedAtUtc;
    }

    // EF Core constructor
    private BodyWeightEntry() { }

    public UserId UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Kg { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    public static BodyWeightEntry Create(UserId userId, DateOnly date, decimal kg, DateTimeOffset recordedAt)
    {
        CheckKg(kg);
        return new BodyWeightEntry(new BodyWeightEntryId(Guid.NewGuid()), userId, date, kg, recordedAt.ToUniversalTime());
    }

    public void Update(decimal kg, DateTimeOffset at)
    {
        CheckKg(kg);
        Kg = kg;
        RecordedAtUtc = at.ToUniversalTime();
    }

    private static void CheckKg(decimal kg) =>
        CheckRule(kg >= MinKg && kg <= MaxKg, $"Body weight must be between {MinKg} and {MaxKg} kg.");
}
