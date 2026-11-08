using A2S.Application.Common;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>Hides a habit from the daily list and the streak without deleting its history.</summary>
public sealed record ArchiveHabitCommand(Guid HabitId) : ICommand<Result>;

/// <summary>Brings an archived habit back into the daily list.</summary>
public sealed record UnarchiveHabitCommand(Guid HabitId) : ICommand<Result>;

public sealed class ArchiveHabitCommandValidator : AbstractValidator<ArchiveHabitCommand>
{
    public ArchiveHabitCommandValidator() => RuleFor(x => x.HabitId).NotEmpty();
}

public sealed class UnarchiveHabitCommandValidator : AbstractValidator<UnarchiveHabitCommand>
{
    public UnarchiveHabitCommandValidator() => RuleFor(x => x.HabitId).NotEmpty();
}
