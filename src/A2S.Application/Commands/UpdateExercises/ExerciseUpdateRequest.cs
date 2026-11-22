using System.Text.Json.Serialization;
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
    public string? Reason { get; init; }

    /// <summary>Pairs the exercise into this superset group on its day.</summary>
    public int? SupersetGroup { get; init; }

    /// <summary>True takes the exercise out of its superset.</summary>
    public bool ClearSupersetGroup { get; init; }

    /// <summary>Bonus set side (unilateral reps-per-set only).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ExtraSetSide>))]
    public ExtraSetSide? ExtraSetSide { get; init; }
}
