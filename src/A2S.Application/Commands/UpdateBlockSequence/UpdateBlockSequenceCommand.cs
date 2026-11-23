using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.UpdateBlockSequence;

[ChangesSyncArea(SyncArea.Workout)]
public sealed record UpdateBlockSequenceCommand(
    Guid WorkoutId,
    List<int> BlockSequence
) : ICommand<Result<UpdateBlockSequenceResult>>;

