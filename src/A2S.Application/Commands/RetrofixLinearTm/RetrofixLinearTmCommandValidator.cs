using FluentValidation;

namespace A2S.Application.Commands.RetrofixLinearTm;

public sealed class RetrofixLinearTmCommandValidator : AbstractValidator<RetrofixLinearTmCommand>
{
    public RetrofixLinearTmCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.OriginalStartingTm).GreaterThan(0);
    }
}
