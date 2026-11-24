using A2S.Application.Cardio;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Application.Tests.Cardio;

public class CardioSummaryFormatterTests
{
    private static readonly CardioLanePreset Stairs = CardioLanes.StairClimber();

    private static PlannedCardioBlock Continuous(int minutes) => PlannedCardioBlock.From(
        Stairs.Lane, Stairs.Stages, Stairs.WarmUp, Stairs.CoolDown, Stairs.RequiresSpeed,
        new CardioStageState(0, minutes, null, 6m, null, null, 0, 0, [], new Dictionary<int, decimal>()));

    private static PlannedCardioBlock Intervals() => PlannedCardioBlock.From(
        Stairs.Lane, Stairs.Stages, Stairs.WarmUp, Stairs.CoolDown, Stairs.RequiresSpeed,
        new CardioStageState(1, null, 0, 8m, null, null, 0, 0, [], new Dictionary<int, decimal>()));

    [Fact]
    public void Continuous_UsesTheLoggedWorkMinutes()
    {
        CardioSummaryFormatter.Format(Continuous(6), new CardioResult(true, EffortRating.Easy, workMinutes: 7m))
            .Should().Be("7 min @ level 6 · Easy");
    }

    [Fact]
    public void Continuous_WithoutLoggedMinutes_UsesThePlan()
    {
        CardioSummaryFormatter.Format(Continuous(6), new CardioResult(true, EffortRating.Solid))
            .Should().Be("6 min @ level 6 · Solid");
    }

    [Fact]
    public void Intervals_UseThePlannedTitle()
    {
        CardioSummaryFormatter.Format(Intervals(), new CardioResult(true, EffortRating.Brutal, workMinutes: 9m))
            .Should().Be("4 × 2 min @ L8 / 2 min @ L3 · Brutal");
    }

    [Fact]
    public void StoppedEarlyWithPain_SaysBoth()
    {
        CardioSummaryFormatter.Format(Continuous(6), new CardioResult(false, EffortRating.Solid, PainFlag.Niggle, workMinutes: 4m))
            .Should().Be("4 min @ level 6 · Solid · stopped early · pain");
    }
}
