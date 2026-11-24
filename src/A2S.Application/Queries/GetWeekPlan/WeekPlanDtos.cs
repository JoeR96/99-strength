using System.Text.Json.Serialization;

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

    /// <summary>
    /// The session's cardio finisher as a planned item after the exercises (null when the plan attaches none).
    /// A track's prescription is its current state, so it is only given for the current week and later.
    /// </summary>
    public A2S.Application.DTOs.PlannedCardioDto? Cardio { get; init; }

    /// <summary>The plan slot's label for this A2S2 day ("Upper A"), when a plan is linked.</summary>
    public string? SessionLabel { get; init; }

    /// <summary>The slot's warm-up routine, when one is set.</summary>
    public Guid? WarmUpRoutineId { get; init; }

    /// <summary>The home routine the plan pairs with this session (a mobility slot on the same weekday), e.g. "Hips &amp; lower back".</summary>
    public Guid? PairedRoutineId { get; init; }

    /// <summary>True when this is the workout's current week and day (the next session to do in the cycle).</summary>
    public bool IsCurrent { get; init; }
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

    /// <summary>
    /// Hevy exercise template ID for syncing.
    /// Serialized as "hevyExerciseTemplateId" to match the frontend contract used by Hevy sync.
    /// </summary>
    [JsonPropertyName("hevyExerciseTemplateId")]
    public string ExternalTemplateId { get; init; } = string.Empty;
    public string ProgressionType { get; init; } = string.Empty;

    /// <summary>Rest between sets for gym mode's timer: main lifts 180 s, everything else 90 s (<c>RestDefaults</c>).</summary>
    public int RestSeconds { get; init; }
    public IReadOnlyList<PlannedSetDto> PlannedSets { get; init; } = Array.Empty<PlannedSetDto>();
    public PlannedExerciseMetadataDto Metadata { get; init; } = new();

    /// <summary>Superset pairing on the day: same number = A (lower order) then B, one rest after B.</summary>
    public int? SupersetGroup { get; init; }

    /// <summary>None, Left or Right: one bonus set on that side, logged but never part of progression.</summary>
    public string ExtraSetSide { get; init; } = "None";

    /// <summary>A cardio exercise's session (warm-up, work, cool-down). Null for lifts; a cardio exercise has no planned sets.</summary>
    public PlannedCardioBlockDto? CardioBlock { get; init; }
}

/// <summary>
/// Today's prescription for a cardio exercise. <see cref="Text"/> is the whole session,
/// e.g. "3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)". Warm-up and cool-down carry minutes and level (zone null).
/// </summary>
public sealed record PlannedCardioBlockDto(
    string Lane,
    string StageName,
    int StageNumber,
    int StageCount,
    A2S.Application.DTOs.TrackSegmentDto? WarmUp,
    A2S.Application.DTOs.TrackSegmentDto? CoolDown,
    string Kind,
    int WorkMinutes,
    IntervalFormatDto? Intervals,
    decimal? RecoveryLevel,
    string IntensityUnit,
    decimal? Intensity,
    decimal? SuggestedIntensity,
    decimal? Speed,
    bool IsSettingsPending,
    int TotalMinutes,
    string Text);

/// <summary>Interval rounds: hard minutes, then easy minutes between rounds.</summary>
public sealed record IntervalFormatDto(int Rounds, int HardMinutes, int EasyMinutes);

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
    public int? RepRangeMaximum { get; init; }
    public bool IsUnilateral { get; init; }
    public int? TargetTotalReps { get; init; }
    public string? Notes { get; init; }
    public bool IsWeightPending { get; init; }

    /// <summary>A2S2 exercise without a training max yet: its planned sets carry no weight.</summary>
    public bool IsTrainingMaxPending { get; init; }

    /// <summary>A2S2 only: this week's intensity for the exercise's tier (0.70 = 70 %).</summary>
    public decimal? IntensityPercentage { get; init; }

    /// <summary>A2S2 only: Primary or Auxiliary.</summary>
    public string? Tier { get; init; }

    /// <summary>Cardio only: the level/incline (or the walk's speed) is not set yet; gym mode asks for it.</summary>
    public bool IsCardioSettingsPending { get; init; }
}
