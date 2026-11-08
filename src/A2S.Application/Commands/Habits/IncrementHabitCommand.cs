using A2S.Application.Common;
using A2S.Application.DTOs;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>
/// The +1 / -1 tap. Creates the day's log when there is none; a decrement never goes below 0 and an
/// increment past the target is kept (7/6). For a checkbox habit +1 ticks and -1 unticks.
/// </summary>
public sealed record IncrementHabitCommand(
    Guid HabitId,
    DateOnly Date,
    int Delta,
    DateTimeOffset ClientTimestamp) : ICommand<Result<HabitLogDto>>;

public sealed class IncrementHabitCommandValidator : AbstractValidator<IncrementHabitCommand>
{
    public IncrementHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.Delta).Must(d => d is 1 or -1).WithMessage("Delta must be +1 or -1.");
    }
}
