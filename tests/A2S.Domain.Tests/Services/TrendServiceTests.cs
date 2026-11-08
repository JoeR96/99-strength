using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

/// <summary>
/// Brief § 5: 95 kg on 9 Nov 2026, 92 kg by 7 Dec, 91 kg by 21 Dec, hold to 3 Jan, 88 kg by 1 Feb,
/// 85 kg by 8 Mar 2027. Pace is read against the straight line between neighbouring checkpoints.
/// </summary>
public class TrendServiceTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 7, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Start = new(2026, 11, 9);
    private const decimal StartKg = 95m;

    private static readonly IReadOnlyList<(DateOnly Date, decimal Kg)> Checkpoints =
    [
        (new DateOnly(2026, 12, 7), 92m),
        (new DateOnly(2026, 12, 21), 91m),
        (new DateOnly(2027, 1, 3), 91m),
        (new DateOnly(2027, 2, 1), 88m),
        (new DateOnly(2027, 3, 8), 85m)
    ];

    private readonly TrendService _service = new();

    private static BodyWeightEntry Entry(DateOnly date, decimal kg) => BodyWeightEntry.Create(Joe, date, kg, At);

    [Theory]
    [InlineData("2026-11-09", 95.0)]
    [InlineData("2026-12-07", 92.0)]
    [InlineData("2026-12-21", 91.0)]
    [InlineData("2027-01-03", 91.0)]
    [InlineData("2027-02-01", 88.0)]
    [InlineData("2027-03-08", 85.0)]
    public void PaceAgainst_OnCheckpointDates_TargetIsTheCheckpoint(string today, decimal expectedTarget)
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, DateOnly.Parse(today), trendKg: expectedTarget);

        result.TargetKgToday.Should().Be(expectedTarget);
        result.Pace.Should().Be(Pace.OnPace);
        result.DeltaKg.Should().Be(0);
    }

    [Fact]
    public void PaceAgainst_BetweenCheckpoints_InterpolatesLinearly()
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, new DateOnly(2026, 12, 14), trendKg: 91.5m);

        result.TargetKgToday.Should().Be(91.5m);
        result.Pace.Should().Be(Pace.OnPace);
    }

    [Theory]
    [InlineData("2026-12-22")]
    [InlineData("2026-12-28")]
    [InlineData("2027-01-03")]
    public void PaceAgainst_MaintenanceWindow_Holds91(string today)
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, DateOnly.Parse(today), trendKg: 91m);

        result.TargetKgToday.Should().Be(91m);
    }

    [Fact]
    public void PaceAgainst_AfterLastCheckpoint_TargetHolds()
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, new DateOnly(2027, 4, 1), trendKg: 85m);

        result.TargetKgToday.Should().Be(85m);
    }

    [Fact]
    public void PaceAgainst_BeforeStart_TargetIsStartWeight()
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, new DateOnly(2026, 11, 1), trendKg: 95m);

        result.TargetKgToday.Should().Be(95m);
    }

    [Theory]
    [InlineData(92.5, Pace.Behind, 0.5)]
    [InlineData(92.31, Pace.Behind, 0.31)]
    [InlineData(92.3, Pace.OnPace, 0.3)]
    [InlineData(92.0, Pace.OnPace, 0.0)]
    [InlineData(91.7, Pace.OnPace, -0.3)]
    [InlineData(91.5, Pace.Ahead, -0.5)]
    public void PaceAgainst_Tolerance_IsPlusMinusPointThree(decimal trendKg, Pace expected, decimal expectedDelta)
    {
        var result = _service.PaceAgainst(Checkpoints, Start, StartKg, new DateOnly(2026, 12, 7), trendKg);

        result.Pace.Should().Be(expected);
        result.DeltaKg.Should().Be(expectedDelta);
        result.TrendKg.Should().Be(trendKg);
    }

    [Fact]
    public void PaceAgainst_NoCheckpoints_IsNoTarget()
    {
        var result = _service.PaceAgainst([], Start, StartKg, new DateOnly(2026, 12, 7), trendKg: 93m);

        result.Pace.Should().Be(Pace.NoTarget);
        result.TargetKgToday.Should().Be(StartKg);
    }

    [Fact]
    public void Series_Ewma_SeededWithFirstWeight()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 11, 11), 96m),
            Entry(new DateOnly(2026, 11, 9), 95m),
            Entry(new DateOnly(2026, 11, 10), 94m)
        };

        var points = _service.Series(entries);

        points.Select(p => p.Date).Should().BeInAscendingOrder();
        points.Select(p => p.Kg).Should().Equal(95m, 94m, 96m);
        points.Select(p => p.Ewma).Should().Equal(95m, 94.9m, 95.01m);
    }

    [Fact]
    public void Series_MovingAverage7_NullUntilSevenEntriesInTheWindow()
    {
        var entries = Enumerable.Range(0, 8)
            .Select(i => Entry(new DateOnly(2026, 11, 9).AddDays(i), 95m - i * 0.1m))
            .ToList();

        var points = _service.Series(entries);

        points.Take(6).Select(p => p.MovingAverage7).Should().AllSatisfy(ma => ma.Should().BeNull());
        points[6].MovingAverage7.Should().Be(94.7m); // mean of 95.0 … 94.4
        points[7].MovingAverage7.Should().Be(94.6m); // mean of 94.9 … 94.3
    }

    [Fact]
    public void Series_MovingAverage7_UsesOnlyTheTrailingSevenCalendarDays()
    {
        var days = new[] { 1, 2, 3, 4, 5, 6, 8 }; // 7 Nov is missing
        var entries = days.Select(d => Entry(new DateOnly(2026, 11, d), 95m)).ToList();

        var points = _service.Series(entries);

        points[^1].MovingAverage7.Should().BeNull();
    }

    [Fact]
    public void Series_Empty_IsEmpty()
    {
        _service.Series([]).Should().BeEmpty();
    }

    [Fact]
    public void BodyWeightEntry_KgMustBeBetween30And300()
    {
        var tooLight = () => BodyWeightEntry.Create(Joe, Start, 29.9m, At);
        var tooHeavy = () => BodyWeightEntry.Create(Joe, Start, 300.1m, At);

        tooLight.Should().Throw<BusinessRuleViolationException>();
        tooHeavy.Should().Throw<BusinessRuleViolationException>();
        BodyWeightEntry.Create(Joe, Start, 30m, At).Kg.Should().Be(30m);
        BodyWeightEntry.Create(Joe, Start, 300m, At).Kg.Should().Be(300m);
    }

    [Fact]
    public void BodyWeightEntry_Update_ChangesKgAndTimestamp()
    {
        var entry = BodyWeightEntry.Create(Joe, Start, 95m, At);

        entry.Update(94.6m, At.AddHours(1));

        entry.UserId.Should().Be(Joe);
        entry.Date.Should().Be(Start);
        entry.Kg.Should().Be(94.6m);
        entry.RecordedAtUtc.Should().Be(At.AddHours(1));
    }
}
