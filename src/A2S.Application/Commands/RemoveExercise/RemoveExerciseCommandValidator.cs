using FluentValidation;

namespace A2S.Application.Commands.RemoveExercise;

public sealed class RemoveExerciseCommandValidator : AbstractValidator<RemoveExerciseCommand>
{
    public RemoveExerciseCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
    }
}
