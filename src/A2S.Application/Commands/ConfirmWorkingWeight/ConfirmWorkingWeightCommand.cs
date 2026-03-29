using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.ConfirmWorkingWeight;

public sealed record ConfirmWorkingWeightCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    decimal Weight,
    WeightUnit Unit
) : IWorkoutCommand<Result>, IFailureFactory<Result>
{
    public static Result CreateFailure(string error, ErrorCode code) => Result.Failure(error, code);
}
