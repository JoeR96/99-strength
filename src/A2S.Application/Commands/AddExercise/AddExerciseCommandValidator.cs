using FluentValidation;

namespace A2S.Application.Commands.AddExercise;

public sealed class AddExerciseCommandValidator : AbstractValidator<AddExerciseCommand>
{
    private static readonly string[] ProgressionTypes = ["Linear", "RepsPerSet", "MinimalSets"];

    public AddExerciseCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.Exercise).NotNull();
        When(x => x.Exercise is not null, () =>
        {
            RuleFor(x => x.Exercise.TemplateName).NotEmpty();
            RuleFor(x => x.Exercise.ExternalTemplateId).NotEmpty();
            RuleFor(x => x.Exercise.AssignedDay).IsInEnum();
            RuleFor(x => x.Exercise.OrderInDay).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Exercise.ProgressionType).Must(t => ProgressionTypes.Contains(t))
                .WithMessage("ProgressionType must be Linear, RepsPerSet or MinimalSets.");
            RuleFor(x => x.Exercise.StartingWeight).GreaterThanOrEqualTo(0).When(x => x.Exercise.StartingWeight.HasValue);
            RuleFor(x => x.Exercise.WeightUnit).NotNull().When(x => x.Exercise.StartingWeight.HasValue)
                .WithMessage("WeightUnit is required with a starting weight.");
            RuleFor(x => x.Exercise.StartingSets).InclusiveBetween(1, 10).When(x => x.Exercise.StartingSets.HasValue);
            RuleFor(x => x.Exercise)
                .Must(e => e.RepRangeMinimum is null || e.RepRangeMaximum is null || e.RepRangeMinimum <= e.RepRangeMaximum)
                .WithMessage("RepRangeMinimum must not be above RepRangeMaximum.");
        });
    }
}
