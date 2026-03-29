using FluentValidation;

namespace A2S.Application.Commands.ProgressWeek;

public sealed class ProgressWeekCommandValidator : AbstractValidator<ProgressWeekCommand>
{
    public ProgressWeekCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
    }
}
