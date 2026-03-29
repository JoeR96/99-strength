using A2S.Application.Commands.DeleteWorkout;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class DeleteWorkoutCommandValidatorTests
{
    private readonly DeleteWorkoutCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPass()
    {
        var command = new DeleteWorkoutCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new DeleteWorkoutCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }
}
