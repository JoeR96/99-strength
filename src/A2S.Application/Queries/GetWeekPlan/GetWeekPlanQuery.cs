using A2S.Application.Common;
using MediatR;

namespace A2S.Application.Queries.GetWeekPlan;

/// <summary>
/// Query to get the planned sets for a specific week and day.
/// Returns calculated working weights, sets, and reps based on the domain's progression strategies.
/// This is the single source of truth - frontend should NOT calculate these values.
/// </summary>
public sealed record GetWeekPlanQuery(
    Guid? WorkoutId,
    int WeekNumber,
    int DayNumber) : IRequest<Result<WeekPlanDto>>;

/// <summary>
/// DTO representing the planned workout for a specific week and day.
/// Contains all information needed to display and execute the workout.
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
    public string HevyExerciseTemplateId { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;
    public IReadOnlyList<PlannedSetDto> PlannedSets { get; init; } = Array.Empty<PlannedSetDto>();

    /// <summary>
    /// Additional exercise-specific metadata for UI display.
    /// </summary>
    public PlannedExerciseMetadataDto Metadata { get; init; } = new();
}

/// <summary>
/// DTO representing a single planned set with weight and reps.
/// </summary>
public sealed record PlannedSetDto
{
    public int SetNumber { get; init; }

    /// <summary>
    /// Working weight in kilograms (rounded to gym increments).
    /// </summary>
    public decimal WeightKg { get; init; }

    /// <summary>
    /// Working weight in pounds (rounded to gym increments).
    /// </summary>
    public decimal WeightLbs { get; init; }

    /// <summary>
    /// The original weight unit configured for this exercise.
    /// </summary>
    public string OriginalUnit { get; init; } = "Kilograms";

    public int TargetReps { get; init; }
    public bool IsAmrap { get; init; }
}

/// <summary>
/// Additional metadata for a planned exercise.
/// </summary>
public sealed record PlannedExerciseMetadataDto
{
    /// <summary>
    /// For Linear progression: the Training Max value.
    /// </summary>
    public decimal? TrainingMaxValue { get; init; }
    public string? TrainingMaxUnit { get; init; }

    /// <summary>
    /// For RepsPerSet progression: the rep range.
    /// </summary>
    public int? RepRangeMinimum { get; init; }
    public int? RepRangeTarget { get; init; }
    public int? RepRangeMaximum { get; init; }

    /// <summary>
    /// Whether this is a unilateral exercise (sets done per side).
    /// </summary>
    public bool IsUnilateral { get; init; }

    /// <summary>
    /// For MinimalSets progression: target total reps.
    /// </summary>
    public int? TargetTotalReps { get; init; }

    /// <summary>
    /// Notes for display (e.g., AMRAP target, deload info).
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Whether the starting weight has not yet been confirmed for this exercise.
    /// </summary>
    public bool IsWeightPending { get; init; }
}
