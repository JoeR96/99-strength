using A2S.Application.Commands.ConfirmWorkingWeight;
using A2S.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class ConfirmWorkingWeightCommandValidatorTests
{
    private readonly ConfirmWorkingWeightCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPassValidation()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 55m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.Empty, Guid.NewGuid(), 55m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyExerciseId_ShouldFail()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.NewGuid(), Guid.Empty, 55m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }

    [Fact]
    public void ZeroWeight_ShouldFail()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 0, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }

    [Fact]
    public void NegativeWeight_ShouldFail()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), -5m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }

    [Fact]
    public void InvalidUnit_ShouldFail()
    {
        var command = new ConfirmWorkingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 55m, (WeightUnit)99);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Unit);
    }
}
