using FluentValidation;

namespace A2S.Application.Commands.ConfirmStartingWeight;

public sealed class ConfirmStartingWeightCommandValidator : AbstractValidator<ConfirmStartingWeightCommand>
{
    public ConfirmStartingWeightCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.Weight).GreaterThan(0);
        RuleFor(x => x.Unit).IsInEnum();
    }
}
