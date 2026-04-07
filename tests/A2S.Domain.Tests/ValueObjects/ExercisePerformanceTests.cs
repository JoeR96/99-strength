using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class ExercisePerformanceTests
{
    private static readonly ExerciseId TestExerciseId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly Weight TestWeight = Weight.Kilograms(80m);
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WhenValidValues_ShouldCreateSuccessfully()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.ExerciseId.Should().Be(TestExerciseId);
        performance.PlannedSets.Should().HaveCount(1);
        performance.CompletedSets.Should().HaveCount(1);
        performance.CompletedAt.Should().Be(FixedDate);
        performance.SkipProgression.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenSkipProgressionTrue_ShouldStoreFlag()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate, skipProgression: true);

        performance.SkipProgression.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenNoPlannedSets_ShouldThrow()
    {
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        Action act = () => new ExercisePerformance(TestExerciseId, [], completed, FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*At least one planned set is required*");
    }

    [Fact]
    public void Constructor_WhenNoCompletedSets_ShouldThrow()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };

        Action act = () => new ExercisePerformance(TestExerciseId, planned, [], FixedDate);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*At least one completed set is required*");
    }

    [Fact]
    public void GetAmrapDelta_WhenAmrapSetExists_ShouldReturnCorrectDelta()
    {
        var planned = new[]
        {
            new PlannedSet(1, TestWeight, 5),
            new PlannedSet(2, TestWeight, 5, isAmrap: true)
        };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 5),
            new CompletedSet(2, TestWeight, 8, wasAmrap: true)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetAmrapDelta().Should().Be(3);
    }

    [Fact]
    public void GetAmrapDelta_WhenNoAmrapPlanned_ShouldReturnZero()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetAmrapDelta().Should().Be(0);
    }

    [Fact]
    public void GetAmrapDelta_WhenNoAmrapCompleted_ShouldReturnZero()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5, isAmrap: true) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5, wasAmrap: false) };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetAmrapDelta().Should().Be(0);
    }

    [Fact]
    public void GetAmrapDelta_WhenAmrapFellShort_ShouldReturnNegativeDelta()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5, isAmrap: true) };
        var completed = new[] { new CompletedSet(1, TestWeight, 3, wasAmrap: true) };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetAmrapDelta().Should().Be(-2);
    }

    [Fact]
    public void AllSetsHitMax_WhenAllSetsAtOrAboveMax_ShouldReturnTrue()
    {
        var repRange = RepRange.Create(8, 12);
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 12),
            new CompletedSet(2, TestWeight, 13)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.AllSetsHitMax(repRange).Should().BeTrue();
    }

    [Fact]
    public void AllSetsHitMax_WhenAnySetBelowMax_ShouldReturnFalse()
    {
        var repRange = RepRange.Create(8, 12);
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 12),
            new CompletedSet(2, TestWeight, 10)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.AllSetsHitMax(repRange).Should().BeFalse();
    }

    [Fact]
    public void AnySetsBelowMin_WhenOneSetBelowMinimum_ShouldReturnTrue()
    {
        var repRange = RepRange.Create(8, 12);
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 10),
            new CompletedSet(2, TestWeight, 7)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.AnySetsBelowMin(repRange).Should().BeTrue();
    }

    [Fact]
    public void AnySetsBelowMin_WhenAllSetsAtOrAboveMin_ShouldReturnFalse()
    {
        var repRange = RepRange.Create(8, 12);
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 8),
            new CompletedSet(2, TestWeight, 10)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.AnySetsBelowMin(repRange).Should().BeFalse();
    }

    [Fact]
    public void GetTotalRepsCompleted_ShouldSumAllSets()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 10),
            new CompletedSet(2, TestWeight, 8),
            new CompletedSet(3, TestWeight, 7)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetTotalRepsCompleted().Should().Be(25);
    }

    [Fact]
    public void GetSetsUsed_ShouldReturnCompletedSetCount()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 10) };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 10),
            new CompletedSet(2, TestWeight, 8)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.GetSetsUsed().Should().Be(2);
    }

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        var p1 = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);
        var p2 = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        p1.Should().Be(p2);
    }

    [Fact]
    public void Equals_WhenDifferentExerciseId_ShouldNotBeEqual()
    {
        var otherId = new ExerciseId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var planned = new[] { new PlannedSet(1, TestWeight, 5) };
        var completed = new[] { new CompletedSet(1, TestWeight, 5) };

        var p1 = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);
        var p2 = new ExercisePerformance(otherId, planned, completed, FixedDate);

        p1.Should().NotBe(p2);
    }

    [Fact]
    public void Constructor_WhenDifferentSetCountBetweenPlannedAndCompleted_ShouldAllowFlexibility()
    {
        var planned = new[]
        {
            new PlannedSet(1, TestWeight, 5),
            new PlannedSet(2, TestWeight, 5),
            new PlannedSet(3, TestWeight, 5)
        };
        var completed = new[]
        {
            new CompletedSet(1, TestWeight, 5),
            new CompletedSet(2, TestWeight, 5)
        };

        var performance = new ExercisePerformance(TestExerciseId, planned, completed, FixedDate);

        performance.PlannedSets.Should().HaveCount(3);
        performance.CompletedSets.Should().HaveCount(2);
    }
}
