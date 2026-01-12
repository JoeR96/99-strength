using FluentValidation;

namespace A2S.Application.Commands.CreateWorkout;

/// <summary>
/// Validator for CreateWorkoutCommand.
/// </summary>
public sealed class CreateWorkoutCommandValidator : AbstractValidator<CreateWorkoutCommand>
{
    public CreateWorkoutCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Workout name is required")
            .MaximumLength(200)
            .WithMessage("Workout name cannot exceed 200 characters");

        RuleFor(x => x.TotalWeeks)
            .GreaterThan(0)
            .WithMessage("Total weeks must be greater than zero")
            .LessThanOrEqualTo(52)
            .WithMessage("Total weeks cannot exceed 52");

        RuleFor(x => x.Variant)
            .IsInEnum()
            .WithMessage("Invalid program variant");
    }
}
