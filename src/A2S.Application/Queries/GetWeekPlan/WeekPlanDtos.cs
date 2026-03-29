namespace A2S.Application.Queries.GetWeekPlan;

/// <summary>
/// DTO representing the planned workout for a specific week and day.
/// </summary>
public sealed record WeekPlanDto
{
    public Guid WorkoutId { get; init; }
    public string WorkoutName { get; init; } = string.Empty;
    public int WeekNumber { get; init; }
    public int DayNumber { get; init; }
    public int BlockNumber { get; init; }
    public bool IsDeloadWeek { get; init; }
    public decimal IntensityPercentage { get; init; }
    public IReadOnlyList<PlannedExerciseDto> Exercises { get; init; } = Array.Empty<PlannedExerciseDto>();
}

/// <summary>
/// DTO representing a planned exercise with its calculated sets.
/// </summary>
public sealed record PlannedExerciseDto
{
    public Guid ExerciseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Equipment { get; init; } = string.Empty;
    public int OrderInDay { get; init; }
    public string ExternalTemplateId { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;
    public IReadOnlyList<PlannedSetDto> PlannedSets { get; init; } = Array.Empty<PlannedSetDto>();
    public PlannedExerciseMetadataDto Metadata { get; init; } = new();
}

/// <summary>
/// DTO representing a single planned set.
/// </summary>
public sealed record PlannedSetDto
{
    public int SetNumber { get; init; }
    public decimal WeightKg { get; init; }
    public decimal WeightLbs { get; init; }
    public string OriginalUnit { get; init; } = "Kilograms";
    public int TargetReps { get; init; }
    public bool IsAmrap { get; init; }
}

/// <summary>
/// Additional metadata for a planned exercise.
/// </summary>
public sealed record PlannedExerciseMetadataDto
{
    public decimal? TrainingMaxValue { get; init; }
    public string? TrainingMaxUnit { get; init; }
    public int? RepRangeMinimum { get; init; }
    public int? RepRangeTarget { get; init; }
    public int? RepRangeMaximum { get; init; }
    public bool IsUnilateral { get; init; }
    public int? TargetTotalReps { get; init; }
    public string? Notes { get; init; }
    public bool IsWeightPending { get; init; }
}
