using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Strategies.Cardio;

/// <summary>
/// Scripted runs through each lane. Expected texts are worked out by hand from the Global Constraints.
/// </summary>
public class CardioStageGoldenRunTests
{
    private static readonly ExerciseId ExerciseId = new(Guid.Parse("c0c0c0c0-0000-0000-0000-000000000001"));
    private static readonly DateTime FixedDate = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);

    private static readonly CardioResult Easy = new(true, EffortRating.Easy);
    private static readonly CardioResult Solid = new(true, EffortRating.Solid);
    private static readonly CardioResult Brutal = new(true, EffortRating.Brutal);
    private static readonly CardioResult StoppedEarly = new(false, EffortRating.Solid);
    private static readonly CardioResult Niggle = new(true, EffortRating.Easy, PainFlag.Niggle, "knee");

    private static string Stairs(string work) => $"3 min easy (L3) → {work} → 2 min easy (L3)";

    private static void Log(CardioStageStrategy strategy, CardioResult result) =>
        strategy.ApplyPerformanceResult(new ExercisePerformance(
            ExerciseId, [], [], FixedDate, plannedCardio: strategy.CalculateCardioBlock(), cardio: result));

    private static void Run(CardioStageStrategy strategy, IEnumerable<(CardioResult Result, string Expected)> script)
    {
        var step = 0;
        foreach (var (result, expected) in script)
        {
            step++;
            Log(strategy, result);
            strategy.CalculateCardioBlock().Text.Should().Be(expected, $"after scripted session {step}");
        }
    }

    [Fact]
    public void Golden_StairClimber_ScriptedRun()
    {
        var strategy = CardioStageStrategy.Create(CardioLanes.StairClimber());
        strategy.CalculateCardioBlock().Text.Should().Be(Stairs("6 min @ level 6"));

        Run(strategy,
        [
            (Easy, Stairs("7 min @ level 6")),
            (Solid, Stairs("7 min @ level 6")),
            (Solid, Stairs("8 min @ level 6")),
            (Brutal, Stairs("8 min @ level 6")),
            (Solid, Stairs("8 min @ level 6")),
            (Brutal, Stairs("8 min @ level 6")),
            (Solid, Stairs("9 min @ level 6")),
            (StoppedEarly, Stairs("9 min @ level 6")),
            (Niggle, Stairs("8 min @ level 6")),
            (Easy, Stairs("9 min @ level 6")),
            (Easy, Stairs("10 min @ level 6")),
            (Easy, Stairs("11 min @ level 6")),
            (Easy, Stairs("12 min @ level 6")),
            (Easy, Stairs("4 × 2 min @ L? / 2 min @ L3")),
            (Easy, Stairs("4 × 2 min @ L? / 2 min @ L3"))
        ]);

        strategy.IsCardioSettingsPending.Should().BeTrue();
        strategy.CalculateCardioBlock().SuggestedIntensity.Should().Be(8m);
        strategy.SetSettings(8m, null);

        Run(strategy,
        [
            (Easy, Stairs("4 × 3 min @ L8 / 3 min @ L3")),
            (Easy, Stairs("4 × 4 min @ L8 / 3 min @ L3")),
            (Easy, Stairs("4 × 4 min @ L9 / 3 min @ L3")),
            (StoppedEarly, Stairs("4 × 4 min @ L9 / 3 min @ L3")),
            (StoppedEarly, Stairs("4 × 4 min @ L8 / 3 min @ L3")),
            (Niggle, Stairs("4 × 4 min @ L8 / 3 min @ L3")),
            (Niggle, Stairs("4 × 3 min @ L8 / 3 min @ L3")),
            (StoppedEarly, Stairs("4 × 3 min @ L8 / 3 min @ L3")),
            (StoppedEarly, Stairs("4 × 2 min @ L8 / 2 min @ L3")),
            (StoppedEarly, Stairs("4 × 2 min @ L8 / 2 min @ L3")),
            (StoppedEarly, Stairs("12 min @ level 6")),
            (Easy, Stairs("4 × 2 min @ L8 / 2 min @ L3"))
        ]);

        strategy.IsCardioSettingsPending.Should().BeFalse();
    }

    [Fact]
    public void Golden_InclineWalk_ScriptedRun()
    {
        var strategy = CardioStageStrategy.Create(CardioLanes.InclineWalk());
        strategy.CalculateCardioBlock().Text.Should().Be("10 min @ ? % incline");
        strategy.IsCardioSettingsPending.Should().BeTrue();

        Run(strategy, [(Easy, "10 min @ ? % incline")]);

        strategy.SetSettings(5m, 5.5m);
        strategy.CalculateCardioBlock().Text.Should().Be("10 min @ 5 % incline, 5.5 km/h");

        Run(strategy,
        [
            (Easy, "12 min @ 5 % incline, 5.5 km/h"),
            (Solid, "12 min @ 5 % incline, 5.5 km/h"),
            (Brutal, "10 min @ 5 % incline, 5.5 km/h"),
            (Easy, "12 min @ 5 % incline, 5.5 km/h"),
            (Niggle, "12 min @ 5 % incline, 5.5 km/h"),
            (Easy, "14 min @ 5 % incline, 5.5 km/h"),
            (StoppedEarly, "14 min @ 5 % incline, 5.5 km/h"),
            (Niggle, "12 min @ 5 % incline, 5.5 km/h"),
            (Easy, "14 min @ 5 % incline, 5.5 km/h"),
            (Easy, "16 min @ 5 % incline, 5.5 km/h"),
            (Easy, "18 min @ 5 % incline, 5.5 km/h"),
            (Easy, "20 min @ 5 % incline, 5.5 km/h"),
            (Easy, "22 min @ 5 % incline, 5.5 km/h"),
            (Easy, "24 min @ 5 % incline, 5.5 km/h"),
            (Easy, "26 min @ 5 % incline, 5.5 km/h"),
            (Easy, "28 min @ 5 % incline, 5.5 km/h"),
            (Easy, "30 min @ 5 % incline, 5.5 km/h"),
            (Easy, "30 min @ 6 % incline, 5.5 km/h"),
            (Easy, "30 min @ 7 % incline, 5.5 km/h"),
            (Easy, "30 min @ 8 % incline, 5.5 km/h"),
            (Easy, "30 min @ 9 % incline, 5.5 km/h"),
            (Easy, "30 min @ 10 % incline, 5.5 km/h"),
            (Easy, "30 min @ 11 % incline, 5.5 km/h"),
            (Easy, "30 min @ 12 % incline, 5.5 km/h"),
            (Easy, "30 min @ 8 % incline, 5.8 km/h"),
            (Easy, "30 min @ 9 % incline, 5.8 km/h"),
            (Easy, "30 min @ 10 % incline, 5.8 km/h"),
            (Easy, "30 min @ 11 % incline, 5.8 km/h"),
            (Easy, "30 min @ 12 % incline, 5.8 km/h"),
            (Easy, "30 min @ 8 % incline, 6.1 km/h"),
            (Brutal, "30 min @ 12 % incline, 5.8 km/h")
        ]);
    }
}
