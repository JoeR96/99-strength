using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>Raised when a morning weigh-in is recorded or corrected. Hevy's body measurement for the date mirrors it.</summary>
public sealed record BodyWeightRecorded : IDomainEvent
{
    public UserId UserId { get; }
    public DateOnly Date { get; }
    public decimal Kg { get; }
    public DateTime OccurredOn { get; }

    public BodyWeightRecorded(UserId userId, DateOnly date, decimal kg, DateTimeOffset recordedAt)
    {
        UserId = userId;
        Date = date;
        Kg = kg;
        OccurredOn = recordedAt.UtcDateTime;
    }
}
