using A2S.Application.Common;
using A2S.Application.DTOs;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>Changes a habit's name, targets, unit, order and icon. The type never changes.</summary>
public sealed record UpdateHabitCommand(
    Guid HabitId,
    string Name,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon) : ICommand<Result<HabitDto>>;

public sealed class UpdateHabitCommandValidator : AbstractValidator<UpdateHabitCommand>
{
    public UpdateHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.Icon).MaximumLength(50);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Minimum).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Target).GreaterThanOrEqualTo(0);
    }
}
