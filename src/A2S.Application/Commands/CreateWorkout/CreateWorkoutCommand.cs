using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.CreateWorkout;

/// <summary>
/// Command to create a new workout program.
/// </summary>
public sealed record CreateWorkoutCommand(
    string Name,
    ProgramVariant Variant,
    int TotalWeeks = 21
) : ICommand<Result<Guid>>;
