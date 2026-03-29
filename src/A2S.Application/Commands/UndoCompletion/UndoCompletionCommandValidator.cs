using FluentValidation;

namespace A2S.Application.Commands.UndoCompletion;

public sealed class UndoCompletionCommandValidator : AbstractValidator<UndoCompletionCommand>
{
    public UndoCompletionCommandValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
    }
}
