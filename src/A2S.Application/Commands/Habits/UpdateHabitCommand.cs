using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Common;
using A2S.Application.DTOs;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>Changes a habit's name, targets, unit, order, icon and (when given) step. The type never changes.</summary>
[ChangesSyncArea(SyncArea.Habits)]
public sealed record UpdateHabitCommand(
    Guid HabitId,
    string Name,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon,
    decimal? Step = null) : ICommand<Result<HabitDto>>;

public sealed class UpdateHabitCommandValidator : AbstractValidator<UpdateHabitCommand>
{
    public UpdateHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.Icon).MaximumLength(50);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Step).GreaterThan(0).LessThanOrEqualTo(A2S.Domain.Aggregates.Habits.HabitDefinition.MaximumStep).When(x => x.Step is not null);
        RuleFor(x => x.Minimum).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Target).GreaterThanOrEqualTo(0);
    }
}
