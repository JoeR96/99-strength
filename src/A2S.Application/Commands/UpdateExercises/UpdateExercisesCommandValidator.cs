using FluentValidation;

namespace A2S.Application.Commands.UpdateExercises;

public sealed class UpdateExercisesCommandValidator : AbstractValidator<UpdateExercisesCommand>
{
    public UpdateExercisesCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.Updates).NotEmpty();
        RuleForEach(x => x.Updates).ChildRules(update =>
        {
            update.RuleFor(x => x.ExerciseId).NotEmpty();
            update.When(x => x.TrainingMaxValue.HasValue, () =>
            {
                update.RuleFor(x => x.TrainingMaxValue!.Value).GreaterThan(0);
            });
            update.When(x => x.WeightValue.HasValue, () =>
            {
                update.RuleFor(x => x.WeightValue!.Value).GreaterThanOrEqualTo(0);
            });
        });
    }
}
