using FluentValidation;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>Bounds on a cardio exercise's result, shared by CompleteDay and the gym-mode finish.</summary>
public sealed class CardioResultRequestValidator : AbstractValidator<CardioResultRequest>
{
    public CardioResultRequestValidator()
    {
        RuleFor(c => c.Effort).IsInEnum();
        RuleFor(c => c.Pain).IsInEnum();
        RuleFor(c => c.PainArea).MaximumLength(100);
        RuleFor(c => c.TotalSeconds).InclusiveBetween(1, 4 * 3600);
        RuleFor(c => c.WorkMinutes).InclusiveBetween(0m, 240m).When(c => c.WorkMinutes.HasValue);
        RuleFor(c => c.Intensity).InclusiveBetween(0m, 100m).When(c => c.Intensity.HasValue);
        RuleFor(c => c.Speed).InclusiveBetween(0m, 25m).When(c => c.Speed.HasValue);
        RuleFor(c => c.Floors).InclusiveBetween(0, 2000).When(c => c.Floors.HasValue);
    }
}
