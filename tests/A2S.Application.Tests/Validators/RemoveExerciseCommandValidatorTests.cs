using A2S.Application.Commands.RemoveExercise;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class RemoveExerciseCommandValidatorTests
{
    private readonly RemoveExerciseCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new RemoveExerciseCommand(
            Guid.Parse("bbb11111-1111-1111-1111-111111111111"),
            Guid.Parse("bbb22222-2222-2222-2222-222222222222"));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new RemoveExerciseCommand(Guid.Empty, Guid.Parse("bbb22222-2222-2222-2222-222222222222"));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyExerciseId_ShouldFail()
    {
        var command = new RemoveExerciseCommand(Guid.Parse("bbb11111-1111-1111-1111-111111111111"), Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }
}
