using A2S.Domain.Enums;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class A2SHypertrophyProgramTests
{
    // --- Constants ---

    [Fact]
    public void TotalWeeks_ShouldBe21()
    {
        A2SHypertrophyProgram.TotalWeeks.Should().Be(21);
    }

    [Fact]
    public void WorkingSets_ShouldBe5()
    {
        A2SHypertrophyProgram.WorkingSets.Should().Be(5);
    }

    [Fact]
    public void DeloadSets_ShouldBe4()
    {
        A2SHypertrophyProgram.DeloadSets.Should().Be(4);
    }

    [Fact]
    public void DeloadReps_ShouldBe5()
    {
        A2SHypertrophyProgram.DeloadReps.Should().Be(5);
    }

    [Fact]
    public void DeloadIntensity_ShouldBe58Percent()
    {
        A2SHypertrophyProgram.DeloadIntensity.Should().Be(0.58m);
    }

    // --- Deload weeks ---

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void IsDeloadWeek_WhenDeloadWeek_ShouldReturnTrue(int week)
    {
        A2SHypertrophyProgram.IsDeloadWeek(week).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(20)]
    public void IsDeloadWeek_WhenWorkingWeek_ShouldReturnFalse(int week)
    {
        A2SHypertrophyProgram.IsDeloadWeek(week).Should().BeFalse();
    }

    // --- Validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(22)]
    [InlineData(100)]
    public void GetWeekData_WhenInvalidWeekNumber_ShouldThrow(int week)
    {
        Action act = () => A2SHypertrophyProgram.GetWeekData(week);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- Deload week data ---

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void GetWeekData_WhenDeloadWeek_ShouldReturnDeloadParameters(int week)
    {
        var data = A2SHypertrophyProgram.GetWeekData(week, ProgramTier.Primary);

        data.Intensity.Should().Be(0.58m);
        data.Sets.Should().Be(4);
        data.RepsPerSet.Should().Be(5);
        data.RepOutTarget.Should().BeNull();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void GetWeekData_WhenDeloadWeek_T2ShouldMatchT1(int week)
    {
        var t1 = A2SHypertrophyProgram.GetWeekData(week, ProgramTier.Primary);
        var t2 = A2SHypertrophyProgram.GetWeekData(week, ProgramTier.Auxiliary);

        t2.Should().Be(t1);
    }

    // --- Working weeks: sets ---

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(20)]
    public void GetSetsForWeek_WhenWorkingWeek_ShouldReturn5(int week)
    {
        A2SHypertrophyProgram.GetSetsForWeek(week).Should().Be(5);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void GetSetsForWeek_WhenDeloadWeek_ShouldReturn4(int week)
    {
        A2SHypertrophyProgram.GetSetsForWeek(week).Should().Be(4);
    }

    // --- T1 Primary: Block 1 (weeks 1-6) ---
    // MC1: weeks 1-3, MC2: weeks 4-6
    // T1 base=7, floor=1
    // MC1: reps = max(1, 7 - 1 - position) = 5, 4, 3
    // MC2: reps = max(1, 7 - 1 - position) = 5, 4, 3 (no B3 adjustment)

    [Theory]
    [InlineData(1, 5)]  // MC1 pos 1: max(1, 7-1-1) = 5
    [InlineData(2, 4)]  // MC1 pos 2: max(1, 7-1-2) = 4
    [InlineData(3, 3)]  // MC1 pos 3: max(1, 7-1-3) = 3
    [InlineData(4, 5)]  // MC2 pos 1: max(1, 7-1-1) = 5
    [InlineData(5, 4)]  // MC2 pos 2: max(1, 7-1-2) = 4
    [InlineData(6, 3)]  // MC2 pos 3: max(1, 7-1-3) = 3
    public void GetRepsPerSet_T1Block1_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Primary).Should().Be(expectedReps);
    }

    // --- T1 Primary: Block 2 (weeks 8-13) ---
    // MC1: weeks 8-10, MC2: weeks 11-13
    // MC1: reps = max(1, 7 - 2 - position) = 4, 3, 2
    // MC2: reps = max(1, 7 - 2 - position) = 4, 3, 2 (no B3 adjustment)

    [Theory]
    [InlineData(8, 4)]   // MC1 pos 1: max(1, 7-2-1) = 4
    [InlineData(9, 3)]   // MC1 pos 2: max(1, 7-2-2) = 3
    [InlineData(10, 2)]  // MC1 pos 3: max(1, 7-2-3) = 2
    [InlineData(11, 4)]  // MC2 pos 1: max(1, 7-2-1) = 4
    [InlineData(12, 3)]  // MC2 pos 2: max(1, 7-2-2) = 3
    [InlineData(13, 2)]  // MC2 pos 3: max(1, 7-2-3) = 2
    public void GetRepsPerSet_T1Block2_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Primary).Should().Be(expectedReps);
    }

    // --- T1 Primary: Block 3 (weeks 15-20) ---
    // MC1: weeks 15-17, MC2: weeks 18-20
    // MC1: reps = max(1, 7 - 3 - position) = 3, 2, 1
    // MC2 block 3: reps = max(1, 7 - 3 - position - 1) = 2, 1, 1

    [Theory]
    [InlineData(15, 3)]  // MC1 pos 1: max(1, 7-3-1) = 3
    [InlineData(16, 2)]  // MC1 pos 2: max(1, 7-3-2) = 2
    [InlineData(17, 1)]  // MC1 pos 3: max(1, 7-3-3) = 1
    [InlineData(18, 2)]  // MC2 B3 pos 1: max(1, 7-3-1-1) = 2
    [InlineData(19, 1)]  // MC2 B3 pos 2: max(1, 7-3-2-1) = 1
    [InlineData(20, 1)]  // MC2 B3 pos 3: max(1, 7-3-3-1) = 0 → floor 1
    public void GetRepsPerSet_T1Block3_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Primary).Should().Be(expectedReps);
    }

    // --- T2 Auxiliary: Block 1 (weeks 1-6) ---
    // T2 base=9, floor=2
    // MC1: reps = max(2, 9 - 1 - position) = 7, 6, 5
    // MC2: reps = max(2, 9 - 1 - position) = 7, 6, 5

    [Theory]
    [InlineData(1, 7)]  // mc1 pos 1: max(2, 9-1-1) = 7
    [InlineData(2, 6)]  // mc1 pos 2: max(2, 9-1-2) = 6
    [InlineData(3, 5)]  // mc1 pos 3: max(2, 9-1-3) = 5
    [InlineData(4, 7)]  // mc2 pos 1
    [InlineData(5, 6)]  // mc2 pos 2
    [InlineData(6, 5)]  // mc2 pos 3
    public void GetRepsPerSet_T2Block1_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Auxiliary).Should().Be(expectedReps);
    }

    // --- T2 Auxiliary: Block 2 (weeks 8-13) ---
    // MC1: reps = max(2, 9 - 2 - position) = 6, 5, 4
    // MC2: reps = max(2, 9 - 2 - position) = 6, 5, 4

    [Theory]
    [InlineData(8, 6)]   // mc1 pos 1: max(2, 9-2-1) = 6
    [InlineData(9, 5)]   // mc1 pos 2: max(2, 9-2-2) = 5
    [InlineData(10, 4)]  // mc1 pos 3: max(2, 9-2-3) = 4
    [InlineData(11, 6)]  // mc2 pos 1
    [InlineData(12, 5)]  // mc2 pos 2
    [InlineData(13, 4)]  // mc2 pos 3
    public void GetRepsPerSet_T2Block2_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Auxiliary).Should().Be(expectedReps);
    }

    // --- T2 Auxiliary: Block 3 (weeks 15-20) ---
    // MC1: reps = max(2, 9 - 3 - position) = 5, 4, 3
    // MC2 block 3: reps = max(2, 9 - 3 - position - 1) = 4, 3, 2

    [Theory]
    [InlineData(15, 5)]  // MC1 pos 1: max(2, 9-3-1) = 5
    [InlineData(16, 4)]  // MC1 pos 2: max(2, 9-3-2) = 4
    [InlineData(17, 3)]  // MC1 pos 3: max(2, 9-3-3) = 3
    [InlineData(18, 4)]  // MC2 B3 pos 1: max(2, 9-3-1-1) = 4
    [InlineData(19, 3)]  // MC2 B3 pos 2: max(2, 9-3-2-1) = 3
    [InlineData(20, 2)]  // MC2 B3 pos 3: max(2, 9-3-3-1) = 2
    public void GetRepsPerSet_T2Block3_ShouldReturnCorrectReps(int week, int expectedReps)
    {
        A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Auxiliary).Should().Be(expectedReps);
    }

    // --- Rep-out targets T1 ---
    // MC1: reps × 2
    // MC2 blocks 1-2: reps × 2 - 1
    // MC2 block 3: reps × 2

    [Theory]
    [InlineData(1, 10)]   // MC1 B1: 5 × 2 = 10
    [InlineData(2, 8)]    // MC1 B1: 4 × 2 = 8
    [InlineData(3, 6)]    // MC1 B1: 3 × 2 = 6
    [InlineData(4, 9)]    // MC2 B1: 5 × 2 - 1 = 9
    [InlineData(5, 7)]    // MC2 B1: 4 × 2 - 1 = 7
    [InlineData(6, 5)]    // MC2 B1: 3 × 2 - 1 = 5
    [InlineData(8, 8)]    // MC1 B2: 4 × 2 = 8
    [InlineData(9, 6)]    // MC1 B2: 3 × 2 = 6
    [InlineData(10, 4)]   // MC1 B2: 2 × 2 = 4
    [InlineData(11, 7)]   // MC2 B2: 4 × 2 - 1 = 7
    [InlineData(12, 5)]   // MC2 B2: 3 × 2 - 1 = 5
    [InlineData(13, 3)]   // MC2 B2: 2 × 2 - 1 = 3
    [InlineData(15, 6)]   // MC1 B3: 3 × 2 = 6
    [InlineData(16, 4)]   // MC1 B3: 2 × 2 = 4
    [InlineData(17, 2)]   // MC1 B3: 1 × 2 = 2
    [InlineData(18, 4)]   // MC2 B3: 2 × 2 = 4 (B3 MC2 uses reps × 2)
    [InlineData(19, 2)]   // MC2 B3: 1 × 2 = 2
    [InlineData(20, 2)]   // MC2 B3: 1 × 2 = 2
    public void GetRepOutTarget_T1_ShouldReturnCorrectTarget(int week, int expectedTarget)
    {
        A2SHypertrophyProgram.GetRepOutTarget(week, ProgramTier.Primary).Should().Be(expectedTarget);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void GetRepOutTarget_WhenDeloadWeek_ShouldReturnNull(int week)
    {
        A2SHypertrophyProgram.GetRepOutTarget(week, ProgramTier.Primary).Should().BeNull();
    }

    // --- Rep-out targets T2 ---

    [Theory]
    [InlineData(1, 14)]   // MC1 B1: 7 × 2 = 14
    [InlineData(2, 12)]   // MC1 B1: 6 × 2 = 12
    [InlineData(3, 10)]   // MC1 B1: 5 × 2 = 10
    [InlineData(4, 13)]   // MC2 B1: 7 × 2 - 1 = 13
    [InlineData(5, 11)]   // MC2 B1: 6 × 2 - 1 = 11
    [InlineData(6, 9)]    // MC2 B1: 5 × 2 - 1 = 9
    [InlineData(8, 12)]   // MC1 B2: 6 × 2 = 12
    [InlineData(9, 10)]   // MC1 B2: 5 × 2 = 10
    [InlineData(10, 8)]   // MC1 B2: 4 × 2 = 8
    [InlineData(11, 11)]  // MC2 B2: 6 × 2 - 1 = 11
    [InlineData(12, 9)]   // MC2 B2: 5 × 2 - 1 = 9
    [InlineData(13, 7)]   // MC2 B2: 4 × 2 - 1 = 7
    [InlineData(15, 10)]  // MC1 B3: 5 × 2 = 10
    [InlineData(16, 8)]   // MC1 B3: 4 × 2 = 8
    [InlineData(17, 6)]   // MC1 B3: 3 × 2 = 6
    [InlineData(18, 8)]   // MC2 B3: 4 × 2 = 8 (B3 MC2 uses reps × 2)
    [InlineData(19, 6)]   // MC2 B3: 3 × 2 = 6
    [InlineData(20, 4)]   // MC2 B3: 2 × 2 = 4
    public void GetRepOutTarget_T2_ShouldReturnCorrectTarget(int week, int expectedTarget)
    {
        A2SHypertrophyProgram.GetRepOutTarget(week, ProgramTier.Auxiliary).Should().Be(expectedTarget);
    }

    // --- Intensity ---

    [Theory]
    [InlineData(1, ProgramTier.Primary, 0.79)]   // 5 reps → 0.79
    [InlineData(2, ProgramTier.Primary, 0.84)]   // 4 reps → 0.84
    [InlineData(3, ProgramTier.Primary, 0.87)]   // 3 reps → 0.87
    [InlineData(10, ProgramTier.Primary, 0.92)]  // 2 reps → 0.92
    [InlineData(17, ProgramTier.Primary, 0.96)]  // 1 rep → 0.96
    [InlineData(1, ProgramTier.Auxiliary, 0.70)]  // 7 reps → 0.70
    [InlineData(2, ProgramTier.Auxiliary, 0.75)]  // 6 reps → 0.75
    [InlineData(3, ProgramTier.Auxiliary, 0.79)]  // 5 reps → 0.79
    public void GetIntensity_ShouldReturnCorrectPercentage(int week, ProgramTier tier, decimal expected)
    {
        A2SHypertrophyProgram.GetIntensity(week, tier).Should().Be(expected);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void GetIntensity_WhenDeloadWeek_ShouldReturn58Percent(int week)
    {
        A2SHypertrophyProgram.GetIntensity(week, ProgramTier.Primary).Should().Be(0.58m);
    }

    // --- Full table verification: all 21 weeks T1 ---

    [Fact]
    public void GetWeekData_T1_AllWorkingWeeks_ShouldHave5Sets()
    {
        for (var week = 1; week <= 21; week++)
        {
            var data = A2SHypertrophyProgram.GetWeekData(week, ProgramTier.Primary);
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                data.Sets.Should().Be(4, $"Week {week} is deload, should have 4 sets");
            }
            else
            {
                data.Sets.Should().Be(5, $"Week {week} is working, should have 5 sets");
            }
        }
    }

    [Fact]
    public void GetWeekData_T1_RepsDecreaseAcrossBlocks()
    {
        // Block 1 week 1 reps should be >= Block 2 week 8 reps >= Block 3 week 15 reps
        var b1Reps = A2SHypertrophyProgram.GetRepsPerSet(1, ProgramTier.Primary);
        var b2Reps = A2SHypertrophyProgram.GetRepsPerSet(8, ProgramTier.Primary);
        var b3Reps = A2SHypertrophyProgram.GetRepsPerSet(15, ProgramTier.Primary);

        b1Reps.Should().BeGreaterThanOrEqualTo(b2Reps);
        b2Reps.Should().BeGreaterThanOrEqualTo(b3Reps);
    }

    [Fact]
    public void GetWeekData_T1_IntensityIncreasesAsRepsDecrease()
    {
        // Higher reps → lower intensity, lower reps → higher intensity
        var week1Intensity = A2SHypertrophyProgram.GetIntensity(1, ProgramTier.Primary);  // 5 reps
        var week17Intensity = A2SHypertrophyProgram.GetIntensity(17, ProgramTier.Primary); // 1 rep

        week17Intensity.Should().BeGreaterThan(week1Intensity);
    }

    // --- Full table verification: all 21 weeks T2 ---

    [Fact]
    public void GetWeekData_T2_AllWorkingWeeks_ShouldHave5Sets()
    {
        for (var week = 1; week <= 21; week++)
        {
            var data = A2SHypertrophyProgram.GetWeekData(week, ProgramTier.Auxiliary);
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                data.Sets.Should().Be(4, $"Week {week} is deload, should have 4 sets");
            }
            else
            {
                data.Sets.Should().Be(5, $"Week {week} is working, should have 5 sets");
            }
        }
    }

    [Fact]
    public void GetWeekData_T2_FloorRepsIs2()
    {
        // T2 floor is 2, so reps should never go below 2
        for (var week = 1; week <= 21; week++)
        {
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                continue;
            }

            var reps = A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Auxiliary);
            reps.Should().BeGreaterThanOrEqualTo(2, $"T2 week {week} reps should be >= 2 (floor)");
        }
    }

    [Fact]
    public void GetWeekData_T1_FloorRepsIs1()
    {
        // T1 floor is 1, so reps should never go below 1
        for (var week = 1; week <= 21; week++)
        {
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                continue;
            }

            var reps = A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Primary);
            reps.Should().BeGreaterThanOrEqualTo(1, $"T1 week {week} reps should be >= 1 (floor)");
        }
    }

    // --- Block boundaries ---

    [Theory]
    [InlineData(1, 1)]   // Week 1 → Block 1
    [InlineData(6, 1)]   // Week 6 → Block 1
    [InlineData(7, 1)]   // Week 7 → Block 1 (deload)
    [InlineData(8, 2)]   // Week 8 → Block 2
    [InlineData(13, 2)]  // Week 13 → Block 2
    [InlineData(14, 2)]  // Week 14 → Block 2 (deload)
    [InlineData(15, 3)]  // Week 15 → Block 3
    [InlineData(20, 3)]  // Week 20 → Block 3
    [InlineData(21, 3)]  // Week 21 → Block 3 (deload)
    public void GetWeekData_ShouldBelongToCorrectBlock(int week, int expectedBlock)
    {
        // Block number is (week - 1) / 7 + 1
        var actualBlock = (week - 1) / A2SHypertrophyProgram.WeeksPerBlock + 1;
        actualBlock.Should().Be(expectedBlock);
    }

    // --- T2 reps always higher than or equal to T1 for same week ---

    [Fact]
    public void GetRepsPerSet_T2AlwaysGreaterThanOrEqualToT1()
    {
        for (var week = 1; week <= 21; week++)
        {
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                continue;
            }

            var t1Reps = A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Primary);
            var t2Reps = A2SHypertrophyProgram.GetRepsPerSet(week, ProgramTier.Auxiliary);

            t2Reps.Should().BeGreaterThanOrEqualTo(t1Reps,
                $"T2 reps ({t2Reps}) should be >= T1 reps ({t1Reps}) at week {week}");
        }
    }

    // --- All working weeks should have non-null rep-out targets ---

    [Fact]
    public void GetRepOutTarget_AllWorkingWeeks_ShouldHaveTargets()
    {
        for (var week = 1; week <= 21; week++)
        {
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                continue;
            }

            A2SHypertrophyProgram.GetRepOutTarget(week, ProgramTier.Primary).Should().NotBeNull(
                $"T1 week {week} should have rep-out target");
            A2SHypertrophyProgram.GetRepOutTarget(week, ProgramTier.Auxiliary).Should().NotBeNull(
                $"T2 week {week} should have rep-out target");
        }
    }

    // --- Rep-out target always >= reps (must rep out above or equal to target reps) ---

    [Fact]
    public void GetRepOutTarget_ShouldAlwaysBeGreaterThanOrEqualToReps()
    {
        for (var week = 1; week <= 21; week++)
        {
            if (A2SHypertrophyProgram.IsDeloadWeek(week))
            {
                continue;
            }

            foreach (var tier in new[] { ProgramTier.Primary, ProgramTier.Auxiliary })
            {
                var reps = A2SHypertrophyProgram.GetRepsPerSet(week, tier);
                var repOut = A2SHypertrophyProgram.GetRepOutTarget(week, tier);

                repOut.Should().BeGreaterThanOrEqualTo(reps,
                    $"Rep-out target ({repOut}) should be >= reps ({reps}) for {tier} week {week}");
            }
        }
    }
}
