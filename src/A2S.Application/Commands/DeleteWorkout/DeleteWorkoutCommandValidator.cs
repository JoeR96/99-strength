using FluentValidation;

namespace A2S.Application.Commands.DeleteWorkout;

public sealed class DeleteWorkoutCommandValidator : AbstractValidator<DeleteWorkoutCommand>
{
    public DeleteWorkoutCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
    }
}
