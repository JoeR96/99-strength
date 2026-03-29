using FluentValidation;

namespace A2S.Application.Commands.SubstituteExercise;

public sealed class SubstituteExerciseCommandValidator : AbstractValidator<SubstituteExerciseCommand>
{
    public SubstituteExerciseCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.NewExerciseName).NotEmpty().MaximumLength(200);

        When(x => x.NewProgressionConfig != null, () =>
        {
            RuleFor(x => x.NewProgressionConfig!.Type).NotEmpty();
        });
    }
}
