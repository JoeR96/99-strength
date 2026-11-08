using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class SessionTimeEstimatorTests
{
    /// <summary>
    /// Upper A from brief § 5, hand-calculated with 40 s of work per set:
    /// bench 5 × (40+180) = 1100 s; chest-supported row 3 × 130 = 390 s;
    /// landmine press per side → 6 sets × 130 = 780 s; superset cable row + face pulls
    /// 3 pairs × (80 + 60) = 420 s. Total 2690 s → 45 min, plus a 10 min warm-up = 55.
    /// </summary>
    private static readonly ExerciseTiming[] UpperA =
    [
        new("Bench Press (Barbell)", 5, RestDefaults.MainLiftSeconds, null),
        new("Chest Supported Row", 3, RestDefaults.AccessorySeconds, null),
        new("Landmine Press", 6, RestDefaults.AccessorySeconds, null),
        new("Single Arm Cable Row", 3, RestDefaults.SupersetSeconds, 1),
        new("Face Pull", 3, RestDefaults.SupersetSeconds, 1)
    ];

    [Fact]
    public void RestDefaults_MatchTheBrief()
    {
        RestDefaults.MainLiftSeconds.Should().Be(180);
        RestDefaults.AccessorySeconds.Should().Be(90);
        RestDefaults.SupersetSeconds.Should().Be(60);
    }

    [Fact]
    public void Estimate_UpperA_LandsOnTheFiftyFiveMinuteBudget()
    {
        var estimate = SessionTimeEstimator.Estimate(UpperA);

        estimate.TotalMinutes.Should().Be(55);
        estimate.TotalMinutes.Should().BeInRange(50, 60, "the brief allows ±10 % around 55");
        estimate.WarmUpMinutes.Should().Be(10);
        estimate.BudgetMinutes.Should().Be(55);
        estimate.OverBudget.Should().BeFalse();
    }

    [Fact]
    public void Estimate_UpperA_ReportsMinutesPerExercise()
    {
        var estimate = SessionTimeEstimator.Estimate(UpperA);

        estimate.PerExercise.Should().Equal(
            ("Bench Press (Barbell)", 19),   // 1100 s
            ("Chest Supported Row", 7),      // 390 s
            ("Landmine Press", 13),          // 780 s
            ("Single Arm Cable Row", 4),     // 120 s work + half of 180 s shared rest = 210 s
            ("Face Pull", 4));
    }

    [Fact]
    public void Estimate_AddingAThreeSetAccessory_FlipsOverBudget()
    {
        var exercises = UpperA.Append(new ExerciseTiming("Cable Fly", 3, RestDefaults.AccessorySeconds, null));

        var estimate = SessionTimeEstimator.Estimate(exercises);

        estimate.TotalMinutes.Should().Be(62);
        estimate.OverBudget.Should().BeTrue();
    }

    [Fact]
    public void Estimate_SupersetCountsRestOncePerRound_AndFinishesTheLongerExerciseAlone()
    {
        ExerciseTiming[] exercises =
        [
            new("A", 3, 60, 1),
            new("B", 2, 90, 1)
        ];

        var estimate = SessionTimeEstimator.Estimate(exercises, warmUpMinutes: 0);

        // Rounds: (A,B) 80 + 90, (A,B) 80 + 90, (A) 40 + 60 → 440 s → 8 min.
        estimate.TotalMinutes.Should().Be(8);
    }

    [Fact]
    public void Estimate_RoundsUpToWholeMinutes()
    {
        var estimate = SessionTimeEstimator.Estimate(
            [new ExerciseTiming("Curl", 1, 21, null)], warmUpMinutes: 0);

        estimate.TotalMinutes.Should().Be(2, "61 seconds rounds up");
    }

    [Fact]
    public void Estimate_WithNoExercises_IsJustTheWarmUp()
    {
        var estimate = SessionTimeEstimator.Estimate([], warmUpMinutes: 10);

        estimate.TotalMinutes.Should().Be(10);
        estimate.PerExercise.Should().BeEmpty();
        estimate.OverBudget.Should().BeFalse();
    }

    [Fact]
    public void Estimate_ExactlyOnBudgetIsNotOver()
    {
        var estimate = SessionTimeEstimator.Estimate(
            [new ExerciseTiming("Bench", 5, 180, null)], warmUpMinutes: 0, budgetMinutes: 19);

        estimate.TotalMinutes.Should().Be(19);
        estimate.OverBudget.Should().BeFalse();
    }

    [Fact]
    public void Estimate_RejectsNegativeSetsOrRest()
    {
        var negativeSets = () => SessionTimeEstimator.Estimate([new ExerciseTiming("A", -1, 60, null)]);
        var negativeRest = () => SessionTimeEstimator.Estimate([new ExerciseTiming("A", 1, -1, null)]);

        negativeSets.Should().Throw<Exception>();
        negativeRest.Should().Throw<Exception>();
    }
}
