using A2S.Application.Commands.UndoCompletion;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class UndoCompletionCommandValidatorTests
{
    private readonly UndoCompletionCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new UndoCompletionCommand(Guid.Parse("eee11111-1111-1111-1111-111111111111"));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new UndoCompletionCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }
}
