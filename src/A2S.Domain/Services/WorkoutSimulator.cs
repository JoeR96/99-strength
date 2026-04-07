using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Services;

/// <summary>
/// Simulates workout progression over time using real progression logic
/// with simulated AMRAP results. Works on cloned state — never modifies
/// the real workout.
/// </summary>
public static class WorkoutSimulator
{
    /// <summary>
    /// Simulates progression for a workout over a specified number of sessions.
    /// Returns time-series data points for each exercise at each simulated step.
    /// </summary>
    /// <param name="workout">The workout to simulate (read-only — state is cloned)</param>
    /// <param name="sessionCount">Number of sessions (day completions) to simulate</param>
    /// <returns>Simulation result with per-exercise time-series data</returns>
    public static SimulationResult Simulate(Workout workout, int sessionCount)
    {
        ArgumentNullException.ThrowIfNull(workout);

        if (sessionCount <= 0 || sessionCount > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionCount), "Session count must be between 1 and 500.");
        }

        // Capture initial state via snapshots
        var exerciseStates = CaptureExerciseStates(workout);
        var result = new SimulationResult
        {
            WorkoutName = workout.Name,
            Variant = workout.Variant.ToString(),
            StartWeek = workout.CurrentWeek,
            TotalWeeks = workout.TotalWeeks,
            ExerciseTimeSeries = workout.Exercises
                .Select(e => new ExerciseSimulationSeries
                {
                    ExerciseId = e.Id.Value,
                    ExerciseName = e.Name,
                    ProgressionType = e.Progression.ProgressionType,
                    DataPoints = new List<SimulationDataPoint>()
                })
                .ToList()
        };

        // Record initial state (point 0)
        RecordDataPoints(result, workout, 0, workout.CurrentWeek, workout.CurrentBlock);

        var simulatedWeek = workout.CurrentWeek;
        var simulatedBlock = workout.CurrentBlock;
        var daysPerWeek = workout.GetDaysPerWeek();
        var completedDaysInWeek = workout.GetCompletedDaysInCurrentWeek().Count;

        try
        {
            for (int session = 1; session <= sessionCount; session++)
            {
                if (simulatedWeek > workout.TotalWeeks)
                {
                    break;
                }

                var templateWeek = workout.GetTemplateWeek(simulatedWeek);
                var blockType = workout.GetBlockType(simulatedWeek);
                var isDeload = A2SHypertrophyProgram.IsDeloadWeek(templateWeek);

                // Simulate each exercise's progression for this session
                foreach (var series in result.ExerciseTimeSeries)
                {
                    var exercise = workout.Exercises.First(e => e.Id.Value == series.ExerciseId);
                    var state = exerciseStates[exercise.Id];

                    SimulateExerciseSession(state, templateWeek, blockType, isDeload);
                }

                completedDaysInWeek++;

                if (completedDaysInWeek >= daysPerWeek)
                {
                    completedDaysInWeek = 0;
                    simulatedWeek++;
                    if (simulatedWeek <= workout.TotalWeeks)
                    {
                        simulatedBlock = workout.GetBlockType(simulatedWeek);
                    }
                }

                RecordDataPointsFromStates(result, exerciseStates, session, simulatedWeek, simulatedBlock);
            }

            result.EndWeek = Math.Min(simulatedWeek, workout.TotalWeeks);
        }
        finally
        {
            // Restore original states so we don't affect the workout
            RestoreExerciseStates(workout, exerciseStates);
        }

        return result;
    }

    private static Dictionary<ExerciseId, ClonedExerciseState> CaptureExerciseStates(Workout workout)
    {
        var states = new Dictionary<ExerciseId, ClonedExerciseState>();

        foreach (var exercise in workout.Exercises)
        {
            var snapshot = exercise.CaptureProgressionSnapshot();
            states[exercise.Id] = new ClonedExerciseState
            {
                ExerciseId = exercise.Id,
                ExerciseName = exercise.Name,
                Snapshot = snapshot,
                Progression = exercise.Progression
            };
        }

        return states;
    }

    private static void RestoreExerciseStates(
        Workout workout,
        Dictionary<ExerciseId, ClonedExerciseState> states)
    {
        foreach (var exercise in workout.Exercises)
        {
            if (states.TryGetValue(exercise.Id, out var state))
            {
                exercise.Progression.RestoreFromSnapshot(state.Snapshot);
            }
        }
    }

    private static void SimulateExerciseSession(
        ClonedExerciseState state,
        int templateWeek,
        int blockType,
        bool isDeload)
    {
        var progression = state.Progression;
        var plannedSets = progression.CalculatePlannedSets(templateWeek, blockType).ToList();

        if (plannedSets.Count == 0)
        {
            return;
        }

        // Simulate completed sets with realistic results
        var completedSets = plannedSets.Select(ps =>
        {
            int simulatedReps;

            if (ps.IsAmrap && !isDeload)
            {
                // AMRAP: hit target ± 2 reps (biased slightly positive for realistic progression)
                simulatedReps = ps.TargetReps + 1;
            }
            else
            {
                simulatedReps = ps.TargetReps;
            }

            return new CompletedSet(ps.SetNumber, ps.Weight, simulatedReps, ps.IsAmrap);
        }).ToList();

        var performance = new ExercisePerformance(
            state.ExerciseId,
            plannedSets,
            completedSets);

        progression.ApplyPerformanceResult(performance);
    }

    private static void RecordDataPoints(
        SimulationResult result,
        Workout workout,
        int sessionIndex,
        int week,
        int block)
    {
        foreach (var series in result.ExerciseTimeSeries)
        {
            var exercise = workout.Exercises.First(e => e.Id.Value == series.ExerciseId);
            var progression = exercise.Progression;

            series.DataPoints.Add(CreateDataPoint(progression, sessionIndex, week, block));
        }
    }

    private static void RecordDataPointsFromStates(
        SimulationResult result,
        Dictionary<ExerciseId, ClonedExerciseState> states,
        int sessionIndex,
        int week,
        int block)
    {
        foreach (var series in result.ExerciseTimeSeries)
        {
            var exerciseId = new ExerciseId(series.ExerciseId);
            var state = states[exerciseId];

            series.DataPoints.Add(CreateDataPoint(state.Progression, sessionIndex, week, block));
        }
    }

    private static SimulationDataPoint CreateDataPoint(
        ExerciseProgression progression,
        int sessionIndex,
        int week,
        int block)
    {
        var tm = progression.GetTrainingMax();
        var weight = progression.GetCurrentWeight();

        return new SimulationDataPoint
        {
            Session = sessionIndex,
            Week = week,
            Block = block,
            TrainingMax = tm?.Value,
            TrainingMaxUnit = tm?.Unit.ToString(),
            CurrentWeight = weight?.Value,
            CurrentWeightUnit = weight?.Unit.ToString(),
            Summary = progression.GetSummary()
        };
    }

    private sealed class ClonedExerciseState
    {
        public required ExerciseId ExerciseId { get; init; }
        public required string ExerciseName { get; init; }
        public required ProgressionSnapshot Snapshot { get; init; }
        public required ExerciseProgression Progression { get; set; }
    }
}

/// <summary>
/// Result of a workout simulation.
/// </summary>
public sealed record SimulationResult
{
    public string WorkoutName { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public int StartWeek { get; init; }
    public int EndWeek { get; set; }
    public int TotalWeeks { get; init; }
    public List<ExerciseSimulationSeries> ExerciseTimeSeries { get; init; } = [];
}

/// <summary>
/// Time-series data for a single exercise across the simulation.
/// </summary>
public sealed record ExerciseSimulationSeries
{
    public Guid ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;
    public List<SimulationDataPoint> DataPoints { get; init; } = [];
}

/// <summary>
/// A single data point in the simulation time-series.
/// </summary>
public sealed record SimulationDataPoint
{
    public int Session { get; init; }
    public int Week { get; init; }
    public int Block { get; init; }
    public decimal? TrainingMax { get; init; }
    public string? TrainingMaxUnit { get; init; }
    public decimal? CurrentWeight { get; init; }
    public string? CurrentWeightUnit { get; init; }
    public required ProgressionSummary Summary { get; init; }
}
