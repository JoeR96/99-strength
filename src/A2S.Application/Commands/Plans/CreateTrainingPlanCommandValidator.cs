using A2S.Domain.Aggregates.Plan;
using FluentValidation;

namespace A2S.Application.Commands.Plans;

public sealed class CreateTrainingPlanCommandValidator : AbstractValidator<CreateTrainingPlanCommand>
{
    public CreateTrainingPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Schedule).NotEmpty().WithMessage("At least one scheduled session is required.");
        RuleForEach(x => x.Schedule).ChildRules(session =>
        {
            session.RuleFor(s => s.Label).NotEmpty().MaximumLength(100);
            session.RuleFor(s => s.Kind).IsInEnum();
            session.RuleFor(s => s.Day).IsInEnum();
        });
        RuleFor(x => x.StartWeightKg)
            .InclusiveBetween(TrainingPlan.MinimumStartWeightKg, TrainingPlan.MaximumStartWeightKg);
        RuleFor(x => x.FlexibleMeals).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AlcoholOccasions).GreaterThanOrEqualTo(0);
    }
}
