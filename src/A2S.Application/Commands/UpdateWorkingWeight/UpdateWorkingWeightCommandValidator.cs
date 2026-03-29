using FluentValidation;

namespace A2S.Application.Commands.UpdateWorkingWeight;

public sealed class UpdateWorkingWeightCommandValidator : AbstractValidator<UpdateWorkingWeightCommand>
{
    public UpdateWorkingWeightCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.NewWeight).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Unit).IsInEnum();
    }
}
