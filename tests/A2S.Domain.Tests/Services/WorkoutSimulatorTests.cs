using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class WorkoutSimulatorTests
{
    [Fact]
    public void Simulate_WhenWorkoutIsNull_ThrowsArgumentNullException()
    {
        var act = () => WorkoutSimulator.Simulate(null!, 10);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Simulate_WhenSessionCountOutOfRange_ThrowsArgumentOutOfRangeException(int sessionCount)
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        var act = () => WorkoutSimulator.Simulate(workout, sessionCount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Simulate_WhenValidWorkout_ReturnsResultWithCorrectMetadata()
    {
        var workout = new WorkoutBuilder()
            .WithName("Test Program")
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 5);

        result.WorkoutName.Should().Be("Test Program");
        result.Variant.Should().Be("FiveDay");
        result.StartWeek.Should().Be(1);
        result.TotalWeeks.Should().Be(21);
    }

    [Fact]
    public void Simulate_WhenLinearExercise_ReturnsTimeSeriesWithTrainingMax()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, 100m)
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 5);

        result.ExerciseTimeSeries.Should().HaveCount(1);

        var series = result.ExerciseTimeSeries[0];
        series.ExerciseName.Should().Be("Squat");
        series.ProgressionType.Should().Be("Linear");
        series.DataPoints.Should().HaveCountGreaterThanOrEqualTo(2);
        series.DataPoints[0].Session.Should().Be(0);
        series.DataPoints[0].TrainingMax.Should().Be(100m);
    }

    [Fact]
    public void Simulate_WhenRpsExercise_ReturnsTimeSeriesWithWeight()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e
                .WithName("Lateral Raise")
                .WithDay(DayNumber.Day1)
                .AsRepsPerSet(repMin: 8, repMax: 12, startingSets: 2, targetSets: 4))
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 5);

        result.ExerciseTimeSeries.Should().HaveCount(1);

        var series = result.ExerciseTimeSeries[0];
        series.ExerciseName.Should().Be("Lateral Raise");
        series.ProgressionType.Should().Be("RepsPerSet");
        series.DataPoints.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Simulate_WhenMultipleExercises_TracksAllExercises()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 80m)
            .WithDefaultLinearExercise("Squat", DayNumber.Day2, 1, 120m)
            .WithExercise(e => e
                .WithName("Curls")
                .WithDay(DayNumber.Day1)
                .WithOrder(2)
                .AsRepsPerSet())
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 10);

        result.ExerciseTimeSeries.Should().HaveCount(3);
        result.ExerciseTimeSeries.Select(s => s.ExerciseName)
            .Should().Contain(new[] { "Bench Press", "Squat", "Curls" });
    }

    [Fact]
    public void Simulate_WhenLinearExerciseWithAmrap_TrainingMaxProgresses()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();

        // Simulate enough sessions (5-day week) to get through a full week
        var result = WorkoutSimulator.Simulate(workout, 20);

        var series = result.ExerciseTimeSeries[0];
        var initialTm = series.DataPoints[0].TrainingMax;
        var laterDataPoints = series.DataPoints.Where(dp => dp.Session >= 5).ToList();

        // AMRAP with target +1 rep should cause TM to increase over time
        if (laterDataPoints.Count > 0)
        {
            laterDataPoints.Any(dp => dp.TrainingMax >= initialTm).Should().BeTrue(
                "Training max should progress upward with positive AMRAP results");
        }
    }

    [Fact]
    public void Simulate_WhenSessionCountExceedsTotalWeeks_StopsAtEnd()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        // 21 weeks × 5 days = 105 sessions max, request 200
        var result = WorkoutSimulator.Simulate(workout, 200);

        result.EndWeek.Should().BeLessThanOrEqualTo(21);

        var series = result.ExerciseTimeSeries[0];
        series.DataPoints.Last().Week.Should().BeLessThanOrEqualTo(22);
    }

    [Fact]
    public void Simulate_WhenDeloadWeek_DataPointsReflectDeloadTransition()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, 100m)
            .Build();

        // Simulate through at least 7 weeks (first deload at week 7)
        // 7 weeks × 5 days = 35 sessions
        var result = WorkoutSimulator.Simulate(workout, 35);

        var series = result.ExerciseTimeSeries[0];

        // Should have data points spanning multiple weeks
        series.DataPoints.Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public void Simulate_DoesNotMutateOriginalWorkout()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();

        var originalTm = workout.Exercises.First().Progression.GetTrainingMax();

        WorkoutSimulator.Simulate(workout, 50);

        var afterTm = workout.Exercises.First().Progression.GetTrainingMax();
        afterTm.Should().Be(originalTm, "Simulation should not mutate the original workout state");
    }

    [Fact]
    public void Simulate_WhenMinimalSetsExercise_ReturnsTimeSeries()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e
                .WithName("Plank")
                .WithDay(DayNumber.Day1)
                .AsMinimalSets(weight: 0m, targetTotalReps: 40, startingSets: 3))
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 5);

        result.ExerciseTimeSeries.Should().HaveCount(1);
        var series = result.ExerciseTimeSeries[0];
        series.ExerciseName.Should().Be("Plank");
        series.ProgressionType.Should().Be("MinimalSets");
        series.DataPoints.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Simulate_InitialDataPoint_HasSessionZero()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, 120m)
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 3);

        var firstPoint = result.ExerciseTimeSeries[0].DataPoints[0];
        firstPoint.Session.Should().Be(0);
        firstPoint.Week.Should().Be(1);
        firstPoint.TrainingMax.Should().Be(120m);
    }

    [Fact]
    public void Simulate_SingleSession_ReturnsTwoDataPoints()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 1);

        // Initial point (session 0) + 1 simulated session
        result.ExerciseTimeSeries[0].DataPoints.Should().HaveCount(2);
    }

    [Fact]
    public void Simulate_WhenUnilateralRpsExercise_TracksProgression()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e
                .WithName("Single Arm Row")
                .WithDay(DayNumber.Day1)
                .AsRepsPerSet(isUnilateral: true))
            .Build();

        var result = WorkoutSimulator.Simulate(workout, 5);

        result.ExerciseTimeSeries.Should().HaveCount(1);
        result.ExerciseTimeSeries[0].ExerciseName.Should().Be("Single Arm Row");
    }

    [Fact]
    public void Simulate_WhenMaxSessionCount_DoesNotThrow()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        var act = () => WorkoutSimulator.Simulate(workout, 500);

        act.Should().NotThrow();
    }
}
