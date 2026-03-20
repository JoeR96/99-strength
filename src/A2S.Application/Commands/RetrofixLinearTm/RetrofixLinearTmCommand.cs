using A2S.Application.Common;

namespace A2S.Application.Commands.RetrofixLinearTm;

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
