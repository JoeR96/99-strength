using FluentValidation;

namespace A2S.Application.Commands.UpdateBlockSequence;

public sealed class UpdateBlockSequenceCommandValidator : AbstractValidator<UpdateBlockSequenceCommand>
{
    public UpdateBlockSequenceCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.BlockSequence).NotEmpty();
        RuleForEach(x => x.BlockSequence).InclusiveBetween(1, 3);
    }
}
