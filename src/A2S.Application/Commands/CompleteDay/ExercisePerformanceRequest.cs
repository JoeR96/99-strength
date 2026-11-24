using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Request data for a single exercise's performance.
/// </summary>
public sealed record ExercisePerformanceRequest
{
    /// <summary>
    /// The ID of the exercise that was performed.
    /// </summary>
    public required Guid ExerciseId { get; init; }

    /// <summary>
    /// The sets completed for this exercise. Empty for a cardio exercise, which sends <see cref="Cardio"/> instead.
    /// </summary>
    public IReadOnlyList<CompletedSetRequest> CompletedSets { get; init; } = [];

    /// <summary>How a cardio exercise went (cardio exercises only; no sets needed).</summary>
    public CardioResultRequest? Cardio { get; init; }

    /// <summary>
    /// Whether this exercise was temporarily substituted for this session only.
    /// When true, progression rules are skipped (no TM changes, no set changes).
    /// The performance is still recorded but the original exercise's progression state is preserved.
    /// </summary>
    public bool WasTemporarySubstitution { get; init; }

    /// <summary>
    /// The bonus set on one side (<c>Exercise.ExtraSetSide</c>). Kept out of <see cref="CompletedSets"/>
    /// on purpose: it is posted to Hevy but never reaches progression evaluation.
    /// </summary>
    public CompletedSetRequest? ExtraSet { get; init; }
}

/// <summary>
/// Request data for a single completed set.
/// </summary>
public sealed record CompletedSetRequest
{
    public required int SetNumber { get; init; }
    public required decimal Weight { get; init; }
    public required WeightUnit WeightUnit { get; init; }
    public required int ActualReps { get; init; }
    public bool WasAmrap { get; init; }
}

/// <summary>
/// A cardio exercise's session: finished or stopped early, the Easy / Solid / Brutal button, any pain, and what was
/// done (total seconds, work minutes, level or incline, speed, floors).
/// </summary>
public sealed record CardioResultRequest(
    bool Finished,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Effort>))] Effort Effort,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PainFlag>))] PainFlag Pain = PainFlag.None,
    string? PainArea = null,
    int TotalSeconds = 0,
    decimal? WorkMinutes = null,
    decimal? Intensity = null,
    decimal? Speed = null,
    int? Floors = null)
{
    /// <summary>The domain result the cardio lane rules read (Easy / Solid / Brutal → RPE 4 / 7 / 9).</summary>
    public CardioResult ToCardioResult() =>
        new(Finished, Effort.ToRating(), Pain, PainArea, TotalSeconds, WorkMinutes, Intensity, Speed, Floors);
}
