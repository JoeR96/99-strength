using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using FluentAssertions;

namespace A2S.Infrastructure.Tests.Persistence;

/// <summary>
/// The jsonb value columns must round-trip every value-object record exactly, including enums as
/// strings, nullable strongly-typed ids and DateOnly, and the comparer must see a reorder as a change.
/// Runs without Docker.
/// </summary>
public class JsonColumnTests
{
    private static List<T> RoundTrip<T>(List<T> list)
    {
        var converter = JsonColumn.Converter<T>();
        var json = (string)converter.ConvertToProvider(list)!;
        return (List<T>)converter.ConvertFromProvider(json)!;
    }

    [Fact]
    public void MobilityDrills_RoundTrip_InOrder()
    {
        var drills = new List<MobilityDrill>
        {
            new("90/90 breathing", 2, DrillDose.Breaths(5), SideBias.None, "ribs down", "back-to-wall breathing", false),
            new("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, "side-on wall rotation", true),
        };

        RoundTrip(drills).Should().Equal(drills);
    }

    [Fact]
    public void CardioIntervals_RoundTrip_WithEnumsAsStrings()
    {
        var intervals = new List<CardioInterval> { new("Warm-up", 600, CardioEffort.WarmUp), new("Work 1", 240, CardioEffort.Work) };
        var json = (string)JsonColumn.Converter<CardioInterval>().ConvertToProvider(intervals)!;

        json.Should().Contain("\"WarmUp\"").And.NotContain("\"effort\":0");
        RoundTrip(intervals).Should().Equal(intervals);
    }

    [Fact]
    public void ScheduledSessions_RoundTrip_WithNullableIdsAndDayOfWeek()
    {
        var cardioId = new CardioProtocolId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessions = new List<ScheduledSession>
        {
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, cardioId, "Norwegian 4x4"),
            ScheduledSession.Rest(DayOfWeek.Sunday, "Rest"),
        };

        var back = RoundTrip(sessions);

        back.Should().Equal(sessions);
        back[1].CardioProtocolId.Should().Be(cardioId);
        back[0].LiftDay.Should().Be(DayNumber.Day1);
    }

    [Fact]
    public void PhasesCheckpointsAndAllowances_RoundTrip_WithDateOnly()
    {
        var phases = new List<PlanPhase> { new(PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3), "Christmas") };
        var checkpoints = new List<Checkpoint> { new(new DateOnly(2026, 12, 7), 92m, "4 cutting weeks"), new(new DateOnly(2027, 3, 8), 85m, null) };
        var uses = new List<AllowanceUse> { new(new DateOnly(2026, 11, 14), AllowanceKind.FlexibleMeal) };

        RoundTrip(phases).Should().Equal(phases);
        RoundTrip(checkpoints).Should().Equal(checkpoints);
        RoundTrip(uses).Should().Equal(uses);
    }

    [Fact]
    public void Comparer_SeesAReorderAsAChange_AndEqualListsAsEqual()
    {
        var comparer = JsonColumn.Comparer<CardioInterval>();
        var a = new List<CardioInterval> { new("Work", 240, CardioEffort.Work), new("Recovery", 180, CardioEffort.Recovery) };
        var b = new List<CardioInterval> { new("Recovery", 180, CardioEffort.Recovery), new("Work", 240, CardioEffort.Work) };

        comparer.Equals(a, b).Should().BeFalse();
        comparer.Equals(a, a.ToList()).Should().BeTrue();
        comparer.Snapshot(a).Should().NotBeSameAs(a).And.Equal(a);
    }

    [Fact]
    public void EmptyOrNullJson_BecomesAnEmptyList()
    {
        var converter = JsonColumn.Converter<Checkpoint>();

        ((List<Checkpoint>)converter.ConvertFromProvider("")!).Should().BeEmpty();
        ((List<Checkpoint>)converter.ConvertFromProvider("[]")!).Should().BeEmpty();
    }
}
