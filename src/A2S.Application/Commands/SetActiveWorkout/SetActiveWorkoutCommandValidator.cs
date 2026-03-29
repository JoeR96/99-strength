using FluentValidation;

namespace A2S.Application.Commands.SetActiveWorkout;

public sealed class SetActiveWorkoutCommandValidator : AbstractValidator<SetActiveWorkoutCommand>
{
    public SetActiveWorkoutCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
    }
}
