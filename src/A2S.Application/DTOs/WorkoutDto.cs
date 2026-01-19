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
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int ExerciseCount { get; init; }
    public IReadOnlyList<ExerciseDto> Exercises { get; init; } = Array.Empty<ExerciseDto>();
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
    public ExerciseProgressionDto Progression { get; init; } = null!;
}

/// <summary>
/// Data Transfer Object for Exercise Progression (polymorphic).
/// </summary>
public record ExerciseProgressionDto
{
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// Data Transfer Object for Linear Progression.
/// </summary>
public sealed record LinearProgressionDto : ExerciseProgressionDto
{
    public decimal TrainingMaxValue { get; init; }
    public string TrainingMaxUnit { get; init; } = string.Empty;
    public bool UseAmrap { get; init; }
    public int BaseSetsPerExercise { get; init; }
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
}
