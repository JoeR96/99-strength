namespace A2S.Application.DTOs;

/// <summary>
/// Data Transfer Object for workout history including completed activities.
/// </summary>
public sealed record WorkoutHistoryDto
{
    public Guid WorkoutId { get; init; }
    public string WorkoutName { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public int TotalWeeks { get; init; }
    public int CurrentWeek { get; init; }
    public int CurrentBlock { get; init; }
    public int DaysPerWeek { get; init; }
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Total number of workouts completed (days × weeks completed).
    /// </summary>
    public int TotalWorkoutsCompleted { get; init; }

    /// <summary>
    /// List of all completed workout activities with performance data.
    /// </summary>
    public IReadOnlyList<WorkoutActivityDto> CompletedActivities { get; init; } = Array.Empty<WorkoutActivityDto>();

    /// <summary>
    /// Summary of exercises with their current progression values and history.
    /// </summary>
    public IReadOnlyList<ExerciseHistoryDto> ExerciseHistories { get; init; } = Array.Empty<ExerciseHistoryDto>();
}

/// <summary>
/// Data Transfer Object for a single completed workout activity.
/// </summary>
public sealed record WorkoutActivityDto
{
    public string Day { get; init; } = string.Empty;
    public int DayNumber { get; init; }
    public int WeekNumber { get; init; }
    public int BlockNumber { get; init; }
    public DateTime CompletedAt { get; init; }
    public bool IsDeloadWeek { get; init; }

    /// <summary>
    /// Performance data for each exercise in this activity.
    /// </summary>
    public IReadOnlyList<ExercisePerformanceDto> Performances { get; init; } = Array.Empty<ExercisePerformanceDto>();
}

/// <summary>
/// Performance data for a single exercise in a workout.
/// </summary>
public sealed record ExercisePerformanceDto
{
    public Guid ExerciseId { get; init; }
    public DateTime CompletedAt { get; init; }
    public IReadOnlyList<CompletedSetDto> CompletedSets { get; init; } = Array.Empty<CompletedSetDto>();
}

/// <summary>
/// A single completed set.
/// </summary>
public sealed record CompletedSetDto
{
    public int SetNumber { get; init; }
    public decimal Weight { get; init; }
    public string WeightUnit { get; init; } = "Kilograms";
    public int ActualReps { get; init; }
    public bool WasAmrap { get; init; }
}

/// <summary>
/// Complete history for a single exercise including week-by-week data.
/// </summary>
public sealed record ExerciseHistoryDto
{
    public Guid ExerciseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;
    public int AssignedDay { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Equipment { get; init; } = string.Empty;

    /// <summary>
    /// Current progression state.
    /// </summary>
    public decimal CurrentWeight { get; init; }
    public string WeightUnit { get; init; } = "Kilograms";
    public int CurrentSets { get; init; }
    public int TargetSets { get; init; }

    /// <summary>
    /// For Linear progression: training max value.
    /// </summary>
    public decimal? TrainingMax { get; init; }

    /// <summary>
    /// Week-by-week performance history.
    /// </summary>
    public IReadOnlyList<WeeklyPerformanceDto> WeeklyHistory { get; init; } = Array.Empty<WeeklyPerformanceDto>();
}

/// <summary>
/// Performance data for a single week.
/// </summary>
public sealed record WeeklyPerformanceDto
{
    public int WeekNumber { get; init; }
    public int BlockNumber { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool IsDeloadWeek { get; init; }

    /// <summary>
    /// Total volume (weight × reps × sets) for this week.
    /// </summary>
    public decimal TotalVolume { get; init; }

    /// <summary>
    /// Average weight used.
    /// </summary>
    public decimal AverageWeight { get; init; }

    /// <summary>
    /// Total reps completed.
    /// </summary>
    public int TotalReps { get; init; }

    /// <summary>
    /// Number of sets completed.
    /// </summary>
    public int SetsCompleted { get; init; }

    /// <summary>
    /// AMRAP reps (if applicable).
    /// </summary>
    public int? AmrapReps { get; init; }

    /// <summary>
    /// Individual sets for this week.
    /// </summary>
    public IReadOnlyList<CompletedSetDto> Sets { get; init; } = Array.Empty<CompletedSetDto>();
}

/// <summary>
/// Aggregated exercise history across all workouts for a specific exercise name.
/// </summary>
public sealed record AggregatedExerciseHistoryDto
{
    public string ExerciseName { get; init; } = string.Empty;

    /// <summary>
    /// Total number of times this exercise has been performed.
    /// </summary>
    public int TotalSessions { get; init; }

    /// <summary>
    /// Total volume across all sessions (weight × reps).
    /// </summary>
    public decimal TotalVolume { get; init; }

    /// <summary>
    /// Total sets completed across all sessions.
    /// </summary>
    public int TotalSets { get; init; }

    /// <summary>
    /// Total reps completed across all sessions.
    /// </summary>
    public int TotalReps { get; init; }

    /// <summary>
    /// Personal record weight.
    /// </summary>
    public decimal PersonalRecordWeight { get; init; }

    /// <summary>
    /// Personal record volume (single set).
    /// </summary>
    public decimal PersonalRecordVolume { get; init; }

    /// <summary>
    /// Weight unit.
    /// </summary>
    public string WeightUnit { get; init; } = "Kilograms";

    /// <summary>
    /// First time performing this exercise.
    /// </summary>
    public DateTime? FirstPerformed { get; init; }

    /// <summary>
    /// Most recent performance.
    /// </summary>
    public DateTime? LastPerformed { get; init; }

    /// <summary>
    /// All sessions for this exercise, ordered by date.
    /// </summary>
    public IReadOnlyList<ExerciseSessionDto> Sessions { get; init; } = Array.Empty<ExerciseSessionDto>();
}

/// <summary>
/// A single session of an exercise from any workout.
/// </summary>
public sealed record ExerciseSessionDto
{
    public Guid WorkoutId { get; init; }
    public string WorkoutName { get; init; } = string.Empty;
    public int WeekNumber { get; init; }
    public int BlockNumber { get; init; }
    public DateTime CompletedAt { get; init; }
    public string ProgressionType { get; init; } = string.Empty;

    /// <summary>
    /// Total volume for this session.
    /// </summary>
    public decimal SessionVolume { get; init; }

    /// <summary>
    /// Sets completed in this session.
    /// </summary>
    public IReadOnlyList<CompletedSetDto> Sets { get; init; } = Array.Empty<CompletedSetDto>();
}
