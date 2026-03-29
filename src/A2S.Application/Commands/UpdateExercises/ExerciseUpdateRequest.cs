using A2S.Domain.Enums;

namespace A2S.Application.Commands.UpdateExercises;

/// <summary>
/// Request to update a single exercise.
/// </summary>
public sealed record ExerciseUpdateRequest
{
    public required Guid ExerciseId { get; init; }
    public decimal? TrainingMaxValue { get; init; }
    public WeightUnit? TrainingMaxUnit { get; init; }
    public decimal? WeightValue { get; init; }
    public WeightUnit? WeightUnit { get; init; }
    public bool? IsUnilateral { get; init; }
    public string? Reason { get; init; }
}
