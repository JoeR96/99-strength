using System.Text.Json.Serialization;
using A2S.Domain.Enums;

namespace A2S.Application.DTOs;

/// <summary>
/// Data Transfer Object for Workout.
/// </summary>
public sealed record WorkoutDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public int TotalWeeks { get; init; }
    public int CurrentWeek { get; init; }
    public int CurrentBlock { get; init; }

    /// <summary>
    /// The current day number (1-6) the user is on within the current week.
    /// </summary>
    public int CurrentDay { get; init; }

    /// <summary>
    /// Number of training days per week based on program variant.
    /// </summary>
    public int DaysPerWeek { get; init; }

    /// <summary>
    /// Days that have been completed in the current week.
    /// </summary>
    public IReadOnlyList<int> CompletedDaysInCurrentWeek { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Whether all days in the current week have been completed.
    /// </summary>
    public bool IsWeekComplete { get; init; }

    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ExerciseCount { get; init; }
    public IReadOnlyList<ExerciseDto> Exercises { get; init; } = Array.Empty<ExerciseDto>();

    /// <summary>
    /// Hevy routine folder ID for organizing routines.
    /// </summary>
    public string? HevyRoutineFolderId { get; init; }

    /// <summary>
    /// Synced Hevy routine IDs by week and day.
    /// </summary>
    public IReadOnlyDictionary<string, string>? HevySyncedRoutines { get; init; }
}

/// <summary>
/// Lightweight Data Transfer Object for Workout list views.
/// </summary>
public sealed record WorkoutSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public int TotalWeeks { get; init; }
    public int CurrentWeek { get; init; }
    public int CurrentBlock { get; init; }
    public int CurrentDay { get; init; }
    public int DaysPerWeek { get; init; }
    public IReadOnlyList<int> CompletedDaysInCurrentWeek { get; init; } = Array.Empty<int>();
    public bool IsWeekComplete { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ExerciseCount { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Data Transfer Object for Exercise.
/// </summary>
public sealed record ExerciseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ExerciseCategory Category { get; init; }
    public EquipmentType Equipment { get; init; }
    public DayNumber AssignedDay { get; init; }
    public int OrderInDay { get; init; }

    /// <summary>
    /// Hevy exercise template ID for syncing.
    /// </summary>
    public string HevyExerciseTemplateId { get; init; } = string.Empty;

    public ExerciseProgressionDto Progression { get; init; } = null!;
}

/// <summary>
/// Data Transfer Object for Exercise Progression (polymorphic).
/// Uses Type discriminator for JSON serialization.
/// </summary>
[JsonDerivedType(typeof(LinearProgressionDto), typeDiscriminator: "Linear")]
[JsonDerivedType(typeof(RepsPerSetProgressionDto), typeDiscriminator: "RepsPerSet")]
[JsonDerivedType(typeof(MinimalSetsProgressionDto), typeDiscriminator: "MinimalSets")]
public record ExerciseProgressionDto
{
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// Data Transfer Object for Linear Progression.
/// </summary>
public sealed record LinearProgressionDto : ExerciseProgressionDto
{
    public TrainingMaxDto TrainingMax { get; init; } = null!;
    public bool UseAmrap { get; init; }
    public int BaseSetsPerExercise { get; init; }
}

/// <summary>
/// Data Transfer Object for Training Max.
/// </summary>
public sealed record TrainingMaxDto
{
    public decimal Value { get; init; }
    public int Unit { get; init; } // 0 = Kilograms, 1 = Pounds
}

/// <summary>
/// Data Transfer Object for Reps Per Set Progression.
/// </summary>
public sealed record RepsPerSetProgressionDto : ExerciseProgressionDto
{
    public RepRangeDto RepRange { get; init; } = null!;
    public int CurrentSetCount { get; init; }
    public int TargetSets { get; init; }
    public decimal CurrentWeight { get; init; }
    public string WeightUnit { get; init; } = string.Empty;
    public bool IsUnilateral { get; init; }
}

/// <summary>
/// Data Transfer Object for Minimal Sets Progression.
/// Used for exercises where the goal is to complete target total reps in minimal sets.
/// </summary>
public sealed record MinimalSetsProgressionDto : ExerciseProgressionDto
{
    public decimal CurrentWeight { get; init; }
    public string WeightUnit { get; init; } = string.Empty;
    public int TargetTotalReps { get; init; }
    public int CurrentSetCount { get; init; }
    public int MinimumSets { get; init; }
    public int MaximumSets { get; init; }
}
