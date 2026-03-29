using A2S.Application.Common;

namespace A2S.Application.Commands.UpdateBlockSequence;

public sealed record UpdateBlockSequenceCommand(
    Guid WorkoutId,
    List<int> BlockSequence
) : ICommand<Result<UpdateBlockSequenceResult>>;

