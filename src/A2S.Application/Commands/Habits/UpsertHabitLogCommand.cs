using A2S.Application.Common;
using A2S.Application.DTOs;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>
/// Sets a habit's value for a date. Last write wins by <paramref name="ClientTimestamp"/>: an older
/// timestamp (an offline device syncing late) does not overwrite a newer value. The stored log is returned
/// either way so the client can reconcile.
/// </summary>
public sealed record UpsertHabitLogCommand(
    Guid HabitId,
    DateOnly Date,
    decimal Value,
    DateTimeOffset ClientTimestamp) : ICommand<Result<HabitLogDto>>;

public sealed class UpsertHabitLogCommandValidator : AbstractValidator<UpsertHabitLogCommand>
{
    public UpsertHabitLogCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}
