using FluentValidation;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Validator for CompleteDayCommand.
/// </summary>
public sealed class CompleteDayCommandValidator : AbstractValidator<CompleteDayCommand>
{
    public CompleteDayCommandValidator()
    {
        RuleFor(x => x.WorkoutId)
            .NotEmpty()
            .WithMessage("Workout ID is required.");

        RuleFor(x => x.Day)
            .IsInEnum()
            .WithMessage("Invalid day number.");

        RuleFor(x => x.Performances)
            .NotEmpty()
            .WithMessage("At least one exercise performance is required.");

        RuleForEach(x => x.Performances)
            .ChildRules(performance =>
            {
                performance.RuleFor(p => p.ExerciseId)
                    .NotEmpty()
                    .WithMessage("Exercise ID is required.");

                performance.RuleFor(p => p.CompletedSets)
                    .NotEmpty()
                    .WithMessage("At least one completed set is required.");

                performance.RuleForEach(p => p.CompletedSets)
                    .ChildRules(set =>
                    {
                        set.RuleFor(s => s.SetNumber)
                            .GreaterThan(0)
                            .WithMessage("Set number must be greater than 0.");

                        set.RuleFor(s => s.Weight)
                            .GreaterThanOrEqualTo(0)
                            .WithMessage("Weight cannot be negative.");

                        set.RuleFor(s => s.ActualReps)
                            .GreaterThanOrEqualTo(0)
                            .WithMessage("Actual reps cannot be negative.");

                        set.RuleFor(s => s.WeightUnit)
                            .IsInEnum()
                            .WithMessage("Invalid weight unit.");
                    });
            });
    }
}
