using FluentValidation;

namespace A2S.Application.Commands.ConfirmWorkingWeight;

public sealed class ConfirmWorkingWeightCommandValidator : AbstractValidator<ConfirmWorkingWeightCommand>
{
    public ConfirmWorkingWeightCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.Weight).GreaterThan(0);
        RuleFor(x => x.Unit).IsInEnum();
    }
}
