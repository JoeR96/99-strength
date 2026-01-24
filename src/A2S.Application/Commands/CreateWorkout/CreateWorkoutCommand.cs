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
    IReadOnlyList<CreateExerciseRequest>? Exercises = null
) : ICommand<Result<Guid>>;

/// <summary>
/// Request to create an exercise with specific configuration.
/// </summary>
public sealed record CreateExerciseRequest
{
    public required string TemplateName { get; init; }
    public required ExerciseCategory Category { get; init; }
    public required string ProgressionType { get; init; } // "Linear", "RepsPerSet", or "MinimalSets"
    public required DayNumber AssignedDay { get; init; }
    public required int OrderInDay { get; init; }

    // For Linear progression
    public decimal? TrainingMaxValue { get; init; }
    public WeightUnit? TrainingMaxUnit { get; init; }

    // For RepsPerSet progression
    public decimal? StartingWeight { get; init; }
    public WeightUnit? WeightUnit { get; init; }
    public int? StartingSets { get; init; }
    public int? TargetSets { get; init; }
    public bool IsUnilateral { get; init; }

    // For MinimalSets progression
    public int? TargetTotalReps { get; init; }
}
