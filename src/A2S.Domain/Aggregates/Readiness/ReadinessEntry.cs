using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Readiness;

/// <summary>Strongly-typed ID for a day's readiness entry.</summary>
public readonly record struct ReadinessEntryId(Guid Value);

/// <summary>The day's recovery colour (WHOOP recovery: green 67–100 %, yellow 34–66 %, red 0–33 %).</summary>
public enum ReadinessColour
{
    Green,
    Yellow,
    Red
}

/// <summary>
/// One tap a day on Today (brief § 5d): how recovered the person feels, stored per user and local date.
/// It only feeds suggestions (skip the finisher and hard cardio on red). It never changes A2S2, RPS,
/// minimal-sets or cardio-track maths. On a red day the finisher is skipped by default; the person can
/// keep it with one tap (<see cref="KeepFinisher"/>) and skip it again.
/// </summary>
public sealed class ReadinessEntry : AggregateRoot<ReadinessEntryId>
{
    private ReadinessEntry(ReadinessEntryId id, UserId userId, DateOnly date, ReadinessColour colour, DateTimeOffset updatedAtUtc)
        : base(id)
    {
        UserId = userId;
        Date = date;
        Colour = colour;
        UpdatedAtUtc = updatedAtUtc;
    }

    // EF Core constructor
    private ReadinessEntry() { }

    public UserId UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public ReadinessColour Colour { get; private set; }

    /// <summary>On a red day, true when the person chose to keep the finisher anyway.</summary>
    public bool KeepFinisher { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>True on a red day unless the person chose to keep the finisher.</summary>
    public bool FinisherSkipped => Colour == ReadinessColour.Red && !KeepFinisher;

    public static ReadinessEntry Create(UserId userId, DateOnly date, ReadinessColour colour, DateTimeOffset at)
    {
        CheckRule(!string.IsNullOrWhiteSpace(userId.Value), "Readiness needs an owner");
        CheckRule(Enum.IsDefined(colour), "Unknown readiness colour");
        return new ReadinessEntry(new ReadinessEntryId(Guid.NewGuid()), userId, date, colour, at.ToUniversalTime());
    }

    /// <summary>Changes the colour. Changing it resets the finisher choice to the colour's default.</summary>
    public void SetColour(ReadinessColour colour, DateTimeOffset at)
    {
        CheckRule(Enum.IsDefined(colour), "Unknown readiness colour");
        if (colour != Colour)
        {
            KeepFinisher = false;
        }

        Colour = colour;
        UpdatedAtUtc = at.ToUniversalTime();
    }

    /// <summary>Keeps (true) or skips (false) the finisher on a red day. Only a red day skips it.</summary>
    public void SetKeepFinisher(bool keep, DateTimeOffset at)
    {
        CheckRule(Colour == ReadinessColour.Red, "Only a red day skips the finisher");
        KeepFinisher = keep;
        UpdatedAtUtc = at.ToUniversalTime();
    }
}

/// <summary>What Today suggests for a readiness colour. Suggestions only: nothing here changes a plan or a track.</summary>
public sealed record ReadinessAdvice(string? Headline, IReadOnlyList<string> Suggestions, bool SkipFinisher, bool SkipHardCardio)
{
    public static ReadinessAdvice For(ReadinessEntry? entry) => entry?.Colour switch
    {
        ReadinessColour.Red => new ReadinessAdvice(
            "Red recovery: take it easy",
            [
                entry.FinisherSkipped ? "Finisher skipped for recovery." : "You kept the finisher. Keep it steady.",
                "Skip hard cardio (stair intervals, 4×4) today.",
                "Walks and home routines are fine."
            ],
            entry.FinisherSkipped,
            SkipHardCardio: true),
        ReadinessColour.Yellow => new ReadinessAdvice(
            "Yellow recovery",
            ["Train as planned and go by feel. Nothing in your plan changes."],
            SkipFinisher: false,
            SkipHardCardio: false),
        ReadinessColour.Green => new ReadinessAdvice(null, [], false, false),
        _ => new ReadinessAdvice(null, [], false, false)
    };
}
