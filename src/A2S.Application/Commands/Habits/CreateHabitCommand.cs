using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>Creates a habit for the current user.</summary>
public sealed record CreateHabitCommand(
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<HabitType>))] HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon = null) : ICommand<Result<HabitDto>>;

public sealed class CreateHabitCommandValidator : AbstractValidator<CreateHabitCommand>
{
    public CreateHabitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Unit).MaximumLength(30);
        RuleFor(x => x.Icon).MaximumLength(50);
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);

        When(x => x.Type != HabitType.Checkbox, () =>
        {
            RuleFor(x => x.Minimum).GreaterThan(0).WithMessage("Minimum must be greater than 0.");
            RuleFor(x => x.Target).GreaterThanOrEqualTo(x => x.Minimum).WithMessage("Target must be at least the minimum.");
        });
    }
}
