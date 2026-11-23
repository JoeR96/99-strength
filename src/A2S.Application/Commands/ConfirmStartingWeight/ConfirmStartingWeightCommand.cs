using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.ConfirmStartingWeight;

[ChangesSyncArea(SyncArea.Workout)]
public sealed record ConfirmStartingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal Weight,
    WeightUnit Unit
) : ICommand<Result>;
