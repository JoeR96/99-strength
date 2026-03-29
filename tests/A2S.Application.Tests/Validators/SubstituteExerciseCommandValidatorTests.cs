using A2S.Application.Commands.SubstituteExercise;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class SubstituteExerciseCommandValidatorTests
{
    private readonly SubstituteExerciseCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPass()
    {
        var command = new SubstituteExerciseCommand(Guid.NewGuid(), Guid.NewGuid(), "Lat Pulldown");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldFail()
    {
        var command = new SubstituteExerciseCommand(Guid.NewGuid(), Guid.NewGuid(), "");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewExerciseName);
    }

    [Fact]
    public void NameTooLong_ShouldFail()
    {
        var command = new SubstituteExerciseCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 201));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewExerciseName);
    }

    [Fact]
    public void WithProgressionConfig_EmptyType_ShouldFail()
    {
        var command = new SubstituteExerciseCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Lat Pulldown",
            NewProgressionConfig: new ProgressionConfigDto { Type = "" });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("NewProgressionConfig.Type");
    }

    [Fact]
    public void WithProgressionConfig_ValidType_ShouldPass()
    {
        var command = new SubstituteExerciseCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Lat Pulldown",
            NewProgressionConfig: new ProgressionConfigDto { Type = "RepsPerSet" });
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
