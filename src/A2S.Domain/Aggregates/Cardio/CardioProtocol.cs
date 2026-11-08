using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>
/// A cardio protocol a person can schedule and log against: Norwegian 4×4, a Couch to 5K week,
/// or a LISS (low-intensity steady state) block. Built through the factory methods only.
/// </summary>
public sealed class CardioProtocol : AggregateRoot<CardioProtocolId>
{
    public const int CouchToFiveKFirstWeek = 1;
    public const int CouchToFiveKLastWeek = 9;
    public const int LissMinimumMinutes = 30;
    public const int LissMaximumMinutes = 40;

    private readonly List<CardioInterval> _intervals = new();

    private CardioProtocol(
        CardioProtocolId id,
        UserId userId,
        string name,
        CardioProtocolKind kind,
        IEnumerable<CardioInterval> intervals,
        string notes,
        int? weekIndex,
        bool repeatWeek)
        : base(id)
    {
        UserId = userId;
        Name = name;
        Kind = kind;
        _intervals = intervals.ToList();
        Notes = notes;
        WeekIndex = weekIndex;
        RepeatWeek = repeatWeek;
    }

    // EF Core constructor
    private CardioProtocol()
    {
        Name = string.Empty;
        Notes = string.Empty;
    }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }
    public CardioProtocolKind Kind { get; private set; }
    public IReadOnlyList<CardioInterval> Intervals => _intervals.AsReadOnly();
    public string Notes { get; private set; }

    /// <summary>Couch to 5K week (1–9). Null for other kinds.</summary>
    public int? WeekIndex { get; private set; }

    /// <summary>When true the next advance stays on the current Couch to 5K week.</summary>
    public bool RepeatWeek { get; private set; }

    /// <summary>Total duration in whole minutes, partial minutes rounded up.</summary>
    public int TotalMinutes => (int)Math.Ceiling(_intervals.Sum(i => i.Seconds) / 60m);

    /// <summary>10 min warm-up, 4 × (4 min work + 3 min active recovery), 5 min cool-down.</summary>
    public static CardioProtocol NorwegianFourByFour(UserId userId)
    {
        var intervals = new List<CardioInterval> { new("Warm-up", 600, CardioEffort.WarmUp) };
        for (var i = 1; i <= 4; i++)
        {
            intervals.Add(new CardioInterval($"Work {i}", 240, CardioEffort.Work));
            intervals.Add(new CardioInterval($"Recovery {i}", 180, CardioEffort.Recovery));
        }
        intervals.Add(new CardioInterval("Cool-down", 300, CardioEffort.CoolDown));

        return new CardioProtocol(
            new CardioProtocolId(Guid.NewGuid()),
            userId,
            "Norwegian 4×4",
            CardioProtocolKind.NorwegianFourByFour,
            intervals,
            "4 × 4 min at 85–95% HRmax with 3 min active recovery between.",
            weekIndex: null,
            repeatWeek: false);
    }

    /// <summary>The standard nine-week Couch to 5K progression, one protocol per week.</summary>
    public static CardioProtocol CouchToFiveK(UserId userId, int weekIndex, bool repeatWeek = false)
    {
        CheckCouchToFiveKWeek(weekIndex);

        return new CardioProtocol(
            new CardioProtocolId(Guid.NewGuid()),
            userId,
            CouchToFiveKName(weekIndex),
            CardioProtocolKind.CouchToFiveK,
            CouchToFiveKIntervals(weekIndex),
            "On grass, track or treadmill where possible.",
            weekIndex,
            repeatWeek);
    }

    /// <summary>One easy block of 30–40 minutes (incline walk, bike) at conversational pace.</summary>
    public static CardioProtocol Liss(UserId userId, int minutes, string? note)
    {
        CheckRule(
            minutes >= LissMinimumMinutes && minutes <= LissMaximumMinutes,
            $"LISS duration must be between {LissMinimumMinutes} and {LissMaximumMinutes} minutes");

        return new CardioProtocol(
            new CardioProtocolId(Guid.NewGuid()),
            userId,
            $"LISS {minutes} min",
            CardioProtocolKind.Liss,
            [new CardioInterval("Easy", minutes * 60, CardioEffort.Easy)],
            note?.Trim() ?? string.Empty,
            weekIndex: null,
            repeatWeek: false);
    }

    /// <summary>Marks the current Couch to 5K week to be repeated on the next advance.</summary>
    public CardioProtocol WithRepeatWeek()
    {
        CheckIsCouchToFiveK();
        RepeatWeek = true;
        return this;
    }

    /// <summary>Clears a pending repeat so the next advance moves on.</summary>
    public CardioProtocol ClearRepeatWeek()
    {
        CheckIsCouchToFiveK();
        RepeatWeek = false;
        return this;
    }

    /// <summary>
    /// Moves a Couch to 5K protocol to the next week. A pending repeat keeps the same week
    /// (and clears the repeat); week 9 stays at week 9.
    /// </summary>
    public CardioProtocol NextWeek()
    {
        CheckIsCouchToFiveK();
        var current = WeekIndex!.Value;

        int next;
        if (RepeatWeek)
        {
            next = current;
            RepeatWeek = false;
        }
        else
        {
            next = Math.Min(current + 1, CouchToFiveKLastWeek);
        }

        WeekIndex = next;
        Name = CouchToFiveKName(next);
        _intervals.Clear();
        _intervals.AddRange(CouchToFiveKIntervals(next));
        return this;
    }

    private void CheckIsCouchToFiveK()
    {
        CheckRule(Kind == CardioProtocolKind.CouchToFiveK && WeekIndex.HasValue,
            "Only Couch to 5K protocols have weeks");
    }

    private static void CheckCouchToFiveKWeek(int weekIndex)
    {
        CheckRule(
            weekIndex >= CouchToFiveKFirstWeek && weekIndex <= CouchToFiveKLastWeek,
            $"Couch to 5K week must be between {CouchToFiveKFirstWeek} and {CouchToFiveKLastWeek}");
    }

    private static string CouchToFiveKName(int weekIndex) => $"Couch to 5K — Week {weekIndex}";

    private static IReadOnlyList<CardioInterval> CouchToFiveKIntervals(int weekIndex)
    {
        var intervals = new List<CardioInterval> { new("Warm-up walk", 300, CardioEffort.WarmUp) };

        switch (weekIndex)
        {
            case 1:
                AddRepeats(intervals, 8, runSeconds: 60, walkSeconds: 90);
                break;
            case 2:
                AddRepeats(intervals, 6, runSeconds: 90, walkSeconds: 120);
                break;
            case 3:
                for (var i = 1; i <= 2; i++)
                {
                    intervals.Add(Run($"Run {i}a", 90));
                    intervals.Add(Walk($"Walk {i}a", 90));
                    intervals.Add(Run($"Run {i}b", 180));
                    intervals.Add(Walk($"Walk {i}b", 180));
                }
                break;
            case 4:
                intervals.Add(Run("Run 1", 180));
                intervals.Add(Walk("Walk 1", 90));
                intervals.Add(Run("Run 2", 300));
                intervals.Add(Walk("Walk 2", 150));
                intervals.Add(Run("Run 3", 180));
                intervals.Add(Walk("Walk 3", 90));
                intervals.Add(Run("Run 4", 300));
                break;
            case 5:
                intervals.Add(Run("Run 1", 300));
                intervals.Add(Walk("Walk 1", 180));
                intervals.Add(Run("Run 2", 300));
                intervals.Add(Walk("Walk 2", 180));
                intervals.Add(Run("Run 3", 300));
                break;
            case 6:
                intervals.Add(Run("Run 1", 300));
                intervals.Add(Walk("Walk 1", 180));
                intervals.Add(Run("Run 2", 480));
                intervals.Add(Walk("Walk 2", 180));
                intervals.Add(Run("Run 3", 300));
                break;
            case 7:
                intervals.Add(Run("Run", 1500));
                break;
            case 8:
                intervals.Add(Run("Run", 1680));
                break;
            case 9:
                intervals.Add(Run("Run", 1800));
                break;
        }

        intervals.Add(new CardioInterval("Cool-down walk", 300, CardioEffort.CoolDown));
        return intervals;
    }

    private static void AddRepeats(List<CardioInterval> intervals, int repeats, int runSeconds, int walkSeconds)
    {
        for (var i = 1; i <= repeats; i++)
        {
            intervals.Add(Run($"Run {i}", runSeconds));
            intervals.Add(Walk($"Walk {i}", walkSeconds));
        }
    }

    private static CardioInterval Run(string label, int seconds) => new(label, seconds, CardioEffort.Work);

    private static CardioInterval Walk(string label, int seconds) => new(label, seconds, CardioEffort.Recovery);
}
