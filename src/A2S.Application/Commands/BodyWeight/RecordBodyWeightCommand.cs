using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.BodyWeight;
using FluentValidation;

namespace A2S.Application.Commands.BodyWeight;

/// <summary>Records (or corrects) the morning weigh-in for a date. One entry per date.</summary>
public sealed record RecordBodyWeightCommand(DateOnly Date, decimal Kg) : ICommand<Result<BodyWeightEntryDto>>;

public sealed class RecordBodyWeightCommandValidator : AbstractValidator<RecordBodyWeightCommand>
{
    public RecordBodyWeightCommandValidator()
    {
        RuleFor(x => x.Kg)
            .InclusiveBetween(BodyWeightEntry.MinKg, BodyWeightEntry.MaxKg)
            .WithMessage($"Body weight must be between {BodyWeightEntry.MinKg} and {BodyWeightEntry.MaxKg} kg.");
    }
}
