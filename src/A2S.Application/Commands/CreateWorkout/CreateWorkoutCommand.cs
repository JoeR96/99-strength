using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.CreateWorkout;

/// <summary>
/// Command to create a new workout program.
/// </summary>
public sealed record CreateWorkoutCommand(
    string Name,
    ProgramVariant Variant,
    int TotalWeeks = 21,
    IReadOnlyList<CreateExerciseRequest>? Exercises = null,
    List<int>? BlockSequence = null
) : ICommand<Result<Guid>>;

/// <summary>
/// Request to create an exercise with specific configuration.
/// </summary>
public sealed record CreateExerciseRequest
{
    public required string TemplateName { get; init; }
    public required string ExternalTemplateId { get; init; }
    public required ExerciseCategory Category { get; init; }
    public required string ProgressionType { get; init; } // "Linear", "RepsPerSet", or "MinimalSets"
    public required DayNumber AssignedDay { get; init; }
    public required int OrderInDay { get; init; }

    // For Linear progression
    public decimal? TrainingMaxValue { get; init; }
    public WeightUnit? TrainingMaxUnit { get; init; }
    /// <summary>
    /// Whether to use AMRAP (As Many Reps As Possible) on the last set.
    /// Defaults to true for all Linear progression exercises.
    /// </summary>
    public bool? UseAmrap { get; init; }

    /// <summary>A2S2 scheme: Primary (T1, default) or Auxiliary (T2).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ProgramTier>))]
    public ProgramTier? Tier { get; init; }

    /// <summary>Start without a training max: planned sets show reps and % until one is entered.</summary>
    public bool TrainingMaxPending { get; init; }

    /// <summary>Superset pairing on the day (same number = done back to back).</summary>
    public int? SupersetGroup { get; init; }

    /// <summary>A bonus set on one side (unilateral reps-per-set only); never part of progression.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ExtraSetSide>))]
    public ExtraSetSide ExtraSetSide { get; init; }

    // For RepsPerSet progression
    public decimal? StartingWeight { get; init; }
    public WeightUnit? WeightUnit { get; init; }
    public int? StartingSets { get; init; }
    public int? TargetSets { get; init; }
    public bool IsUnilateral { get; init; }
    public int? RepRangeMinimum { get; init; }
    public int? RepRangeMaximum { get; init; }

    // For MinimalSets progression
    public int? TargetTotalReps { get; init; }
}
