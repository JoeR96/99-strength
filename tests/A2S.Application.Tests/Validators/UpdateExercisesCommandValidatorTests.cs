using A2S.Application.Commands.UpdateExercises;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class UpdateExercisesCommandValidatorTests
{
    private readonly UpdateExercisesCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPass()
    {
        var command = new UpdateExercisesCommand(Guid.NewGuid(), new[]
        {
            new ExerciseUpdateRequest { ExerciseId = Guid.NewGuid(), TrainingMaxValue = 100m }
        });
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyUpdates_ShouldFail()
    {
        var command = new UpdateExercisesCommand(Guid.NewGuid(), Array.Empty<ExerciseUpdateRequest>());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Updates);
    }

    [Fact]
    public void EmptyExerciseIdInUpdate_ShouldFail()
    {
        var command = new UpdateExercisesCommand(Guid.NewGuid(), new[]
        {
            new ExerciseUpdateRequest { ExerciseId = Guid.Empty, TrainingMaxValue = 100m }
        });
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Updates[0].ExerciseId");
    }
}
