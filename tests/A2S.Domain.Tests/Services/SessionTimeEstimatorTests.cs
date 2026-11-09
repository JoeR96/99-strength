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

    /// <summary>
    /// Lower A (brief § 5): squat 5 sets; RDL 3; rear-foot-elevated split squat 3/side + an extra set
    /// left = 7; superset side plank 3/side + extra left = 7 with Pallof press 3/side = 6.
    /// </summary>
    private static readonly ExerciseTiming[] LowerA =
    [
        new("Squat (Barbell)", 5, RestDefaults.MainLiftSeconds, null),
        new("Romanian Deadlift", 3, RestDefaults.AccessorySeconds, null),
        new("Rear Foot Elevated Split Squat", 7, RestDefaults.AccessorySeconds, null),
        new("Side Plank", 7, RestDefaults.SupersetSeconds, 1),
        new("Pallof Press", 6, RestDefaults.SupersetSeconds, 1)
    ];

    /// <summary>Upper B: OHP 5; lat pulldown 3; incline DB press 3; superset seal row 3 with wall slides 2.</summary>
    private static readonly ExerciseTiming[] UpperB =
    [
        new("Overhead Press (Barbell)", 5, RestDefaults.MainLiftSeconds, null),
        new("Lat Pulldown", 3, RestDefaults.AccessorySeconds, null),
        new("Incline Dumbbell Press", 3, RestDefaults.AccessorySeconds, null),
        new("Seal Row", 3, RestDefaults.SupersetSeconds, 1),
        new("Wall Slides", 2, RestDefaults.SupersetSeconds, 1)
    ];

    /// <summary>Lower B: deadlift 5; leg press 3; single-leg RDL 3/side = 6; superset Copenhagen plank 3/side = 6 with dead bug 3/side = 6.</summary>
    private static readonly ExerciseTiming[] LowerB =
    [
        new("Deadlift (Barbell)", 5, RestDefaults.MainLiftSeconds, null),
        new("Leg Press", 3, RestDefaults.AccessorySeconds, null),
        new("Single Leg Romanian Deadlift", 6, RestDefaults.AccessorySeconds, null),
        new("Copenhagen Plank", 6, RestDefaults.SupersetSeconds, 1),
        new("Dead Bug", 6, RestDefaults.SupersetSeconds, 1)
    ];

    private const int Work = SessionTimeEstimator.DefaultWorkSeconds; // 40 s
    private const int Main = RestDefaults.MainLiftSeconds;            // 180 s
    private const int Acc = RestDefaults.AccessorySeconds;            // 90 s
    private const int Ss = RestDefaults.SupersetSeconds;              // 60 s

    /// <summary>
    /// Brief § 6: the estimate is within ±10 % of a hand calculation for each lifting day. The hand
    /// calculation below follows the estimator's stated rule (sets × (work + rest); a superset round
    /// costs one work period per member plus one rest) and is written out term by term.
    /// </summary>
    public static TheoryData<string, ExerciseTiming[], int> BriefDays()
    {
        // Upper A: 5×(40+180) + 3×(40+90) + 6×(40+90) + 3 rounds × (2×40 + 60) = 1100 + 390 + 780 + 420 = 2690 s
        var upperA = 5 * (Work + Main) + 3 * (Work + Acc) + 6 * (Work + Acc) + 3 * (2 * Work + Ss);
        // Lower A: 1100 + 3×130 + 7×130 + 6 rounds × (2×40 + 60) + 1 round × (40 + 60) = 1100 + 390 + 910 + 840 + 100 = 3340 s
        var lowerA = 5 * (Work + Main) + 3 * (Work + Acc) + 7 * (Work + Acc) + 6 * (2 * Work + Ss) + 1 * (Work + Ss);
        // Upper B: 1100 + 390 + 390 + 2 rounds × 140 + 1 round × 100 = 2260 s
        var upperB = 5 * (Work + Main) + 3 * (Work + Acc) + 3 * (Work + Acc) + 2 * (2 * Work + Ss) + 1 * (Work + Ss);
        // Lower B: 1100 + 390 + 6×130 + 6 rounds × 140 = 1100 + 390 + 780 + 840 = 3110 s
        var lowerB = 5 * (Work + Main) + 3 * (Work + Acc) + 6 * (Work + Acc) + 6 * (2 * Work + Ss);

        static int Minutes(int seconds) => SessionTimeEstimator.DefaultWarmUpMinutes + (int)Math.Ceiling(seconds / 60m);

        return new TheoryData<string, ExerciseTiming[], int>
        {
            { "Upper A", UpperA, Minutes(upperA) }, // 2690 s → 45 + 10 = 55
            { "Lower A", LowerA, Minutes(lowerA) }, // 3340 s → 56 + 10 = 66
            { "Upper B", UpperB, Minutes(upperB) }, // 2260 s → 38 + 10 = 48
            { "Lower B", LowerB, Minutes(lowerB) }  // 3110 s → 52 + 10 = 62
        };
    }

    [Theory]
    [MemberData(nameof(BriefDays))]
    public void Estimate_EachBriefLiftingDay_IsWithinTenPercentOfTheHandCalculation(string day, ExerciseTiming[] exercises, int handMinutes)
    {
        var estimate = SessionTimeEstimator.Estimate(exercises);

        estimate.TotalMinutes.Should().Be(handMinutes, $"{day} follows the estimator's own rule exactly");
        ((decimal)estimate.TotalMinutes).Should().BeInRange(handMinutes * 0.9m, handMinutes * 1.1m, $"{day} must be within ±10 % of the hand calculation");
        estimate.PerExercise.Sum(e => e.Minutes).Should().BeGreaterThanOrEqualTo(estimate.TotalMinutes - estimate.WarmUpMinutes,
            "per-exercise minutes are each rounded up, so they add to at least the total");
    }

    [Fact]
    public void Estimate_HandCalculatedMinutesForTheFourDays_Are55_66_48_62()
    {
        BriefDays().Select(row => (int)row[2]).Should().Equal(55, 66, 48, 62);
    }

    [Fact]
    public void Estimate_FlagsLowerAAndLowerBOverTheFiftyFiveMinuteBudget_ButNotTheUpperDays()
    {
        SessionTimeEstimator.Estimate(UpperA).OverBudget.Should().BeFalse("55 min is on budget");
        SessionTimeEstimator.Estimate(UpperB).OverBudget.Should().BeFalse("48 min");
        SessionTimeEstimator.Estimate(LowerA).OverBudget.Should().BeTrue("66 min: the unilateral extra-left sets add up");
        SessionTimeEstimator.Estimate(LowerB).OverBudget.Should().BeTrue("62 min");
    }

    [Fact]
    public void Estimate_LowerA_SupersetWithUnevenSets_FinishesTheExtraLeftPlankAlone()
    {
        var estimate = SessionTimeEstimator.Estimate(LowerA, warmUpMinutes: 0);

        // Side plank: 7 × 40 s work + 6 shared rests ÷ 2 + 1 whole rest = 280 + 180 + 60 = 520 s → 9 min.
        // Pallof press: 6 × 40 + 6 × 30 = 420 s → 7 min.
        estimate.PerExercise.Should().Contain(("Side Plank", 9));
        estimate.PerExercise.Should().Contain(("Pallof Press", 7));
    }

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
