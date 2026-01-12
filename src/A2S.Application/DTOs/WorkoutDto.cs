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
}
