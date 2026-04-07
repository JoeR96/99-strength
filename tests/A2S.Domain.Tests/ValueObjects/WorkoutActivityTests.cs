using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class WorkoutActivityTests
{
    private static readonly ExerciseId TestExerciseId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly Weight TestWeight = Weight.Kilograms(80m);
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private static ExercisePerformance CreatePerformance()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };
        return new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);
    }

    [Fact]
    public void Constructor_WhenValidValues_ShouldCreateSuccessfully()
    {
        var performances = new[] { CreatePerformance() };

        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, completedAt: FixedDate);

        activity.Day.Should().Be(DayNumber.Day1);
        activity.WeekNumber.Should().Be(1);
        activity.BlockNumber.Should().Be(1);
        activity.Performances.Should().HaveCount(1);
        activity.ProgressionSnapshots.Should().BeEmpty();
        activity.CompletedAt.Should().Be(FixedDate);
    }

    [Fact]
    public void Constructor_WhenWithSnapshots_ShouldStoreSnapshots()
    {
        var performances = new[] { CreatePerformance() };
        var snapshots = new[]
        {
            new ProgressionSnapshot(TestExerciseId, "Bench", "Linear", """{"TrainingMaxValue":100}""")
        };

        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, snapshots, FixedDate);

        activity.ProgressionSnapshots.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WhenWeekNumberIsZero_ShouldThrow()
    {
        Action act = () => new WorkoutActivity(DayNumber.Day1, 0, 1, new[] { CreatePerformance() }, completedAt: FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Week number must be positive*");
    }

    [Fact]
    public void Constructor_WhenBlockNumberIsZero_ShouldThrow()
    {
        Action act = () => new WorkoutActivity(DayNumber.Day1, 1, 0, new[] { CreatePerformance() }, completedAt: FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Block number must be between 1 and 3*");
    }

    [Fact]
    public void Constructor_WhenBlockNumberExceedsThree_ShouldThrow()
    {
        Action act = () => new WorkoutActivity(DayNumber.Day1, 1, 4, new[] { CreatePerformance() }, completedAt: FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Block number must be between 1 and 3*");
    }

    [Fact]
    public void Constructor_WhenNoPerformances_ShouldThrow()
    {
        Action act = () => new WorkoutActivity(DayNumber.Day1, 1, 1, Enumerable.Empty<ExercisePerformance>(), completedAt: FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*At least one exercise performance is required*");
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(14, true)]
    [InlineData(21, true)]
    [InlineData(1, false)]
    [InlineData(6, false)]
    [InlineData(8, false)]
    [InlineData(13, false)]
    public void IsDeloadWeek_ShouldReturnCorrectResult(int weekNumber, bool expected)
    {
        var activity = new WorkoutActivity(DayNumber.Day1, weekNumber, 1, new[] { CreatePerformance() }, completedAt: FixedDate);

        activity.IsDeloadWeek().Should().Be(expected);
    }

    [Fact]
    public void WithReplacedSnapshot_ShouldReturnNewInstanceWithReplacedSnapshot()
    {
        var performances = new[] { CreatePerformance() };
        var originalSnapshot = new ProgressionSnapshot(TestExerciseId, "Bench", "Linear", """{"TrainingMaxValue":100}""");
        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, new[] { originalSnapshot }, FixedDate);

        var replacementSnapshot = new ProgressionSnapshot(TestExerciseId, "Bench", "Linear", """{"TrainingMaxValue":105}""");
        var newActivity = activity.WithReplacedSnapshot(0, replacementSnapshot);

        newActivity.ProgressionSnapshots[0].ProgressionStateJson.Should().Contain("105");
        activity.ProgressionSnapshots[0].ProgressionStateJson.Should().Contain("100");
    }

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var performances = new[] { CreatePerformance() };
        var a1 = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, completedAt: FixedDate);
        var a2 = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, completedAt: FixedDate);

        a1.Should().Be(a2);
    }

    [Fact]
    public void Equals_WhenDifferentWeek_ShouldNotBeEqual()
    {
        var performances = new[] { CreatePerformance() };
        var a1 = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, completedAt: FixedDate);
        var a2 = new WorkoutActivity(DayNumber.Day1, 2, 1, performances, completedAt: FixedDate);

        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Equals_WhenDifferentDay_ShouldNotBeEqual()
    {
        var performances = new[] { CreatePerformance() };
        var a1 = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, completedAt: FixedDate);
        var a2 = new WorkoutActivity(DayNumber.Day2, 1, 1, performances, completedAt: FixedDate);

        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Constructor_WhenNullSnapshots_ShouldDefaultToEmptyList()
    {
        var performances = new[] { CreatePerformance() };

        var activity = new WorkoutActivity(DayNumber.Day1, 1, 1, performances, null, FixedDate);

        activity.ProgressionSnapshots.Should().BeEmpty();
    }
}
