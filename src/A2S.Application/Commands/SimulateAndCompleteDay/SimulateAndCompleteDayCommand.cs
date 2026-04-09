using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;

namespace A2S.Application.Commands.SimulateAndCompleteDay;

/// <summary>
/// Dev-only command that synthesises a fake ExercisePerformance for the workout's
/// CURRENT day and delegates to CompleteDayCommand so the simulation goes through
/// the real progression path and persists state.
///
/// Outcome probabilities are tunable. An optional seed makes runs reproducible.
/// </summary>
public sealed record SimulateAndCompleteDayCommand(
    Guid WorkoutId,
    double SuccessRate = 0.65,
    double MaintainRate = 0.25,
    int? Seed = null
) : ICommand<Result<SimulateAndCompleteDayResult>>;

public sealed record SimulateAndCompleteDayResult
{
    public required CompleteDayResult InnerResult { get; init; }
    public required string OutcomeLabel { get; init; } // "Success", "Maintained", "Failed"
}
