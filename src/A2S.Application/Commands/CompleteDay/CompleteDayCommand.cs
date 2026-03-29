using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Command to complete a training day with exercise performance data.
/// This applies progression rules to each exercise based on actual performance.
/// </summary>
public sealed record CompleteDayCommand(
    Guid WorkoutId,
    DayNumber Day,
    IReadOnlyList<ExercisePerformanceRequest> Performances
) : ICommand<Result<CompleteDayResult>>;
