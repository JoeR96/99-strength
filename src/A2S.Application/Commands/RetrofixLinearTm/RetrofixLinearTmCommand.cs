using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;

namespace A2S.Application.Commands.RetrofixLinearTm;

[ChangesSyncArea(SyncArea.Workout)]
public sealed record RetrofixLinearTmCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal OriginalStartingTm
) : ICommand<Result<List<RetrofixLinearTmResult>>>;

public sealed record RetrofixLinearTmResult(
    int Week,
    decimal OldTm,
    decimal NewTm
);
