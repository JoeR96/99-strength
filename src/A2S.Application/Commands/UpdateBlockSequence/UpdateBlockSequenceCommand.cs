using A2S.Application.Common;
using MediatR;

namespace A2S.Application.Commands.UpdateBlockSequence;

public sealed record UpdateBlockSequenceCommand(
    Guid WorkoutId,
    List<int> BlockSequence
) : IRequest<Result<UpdateBlockSequenceResult>>;

public sealed record UpdateBlockSequenceResult
{
    public Guid WorkoutId { get; init; }
    public List<int> BlockSequence { get; init; } = new();
    public int TotalWeeks { get; init; }
    public int CurrentWeek { get; init; }
    public int CurrentBlock { get; init; }
}
