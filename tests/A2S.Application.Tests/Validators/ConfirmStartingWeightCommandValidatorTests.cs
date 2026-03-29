using A2S.Application.Commands.ConfirmStartingWeight;
using A2S.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class ConfirmStartingWeightCommandValidatorTests
{
    private readonly ConfirmStartingWeightCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPassValidation()
    {
        var command = new ConfirmStartingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 60m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new ConfirmStartingWeightCommand(Guid.Empty, Guid.NewGuid(), 60m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyExerciseId_ShouldFail()
    {
        var command = new ConfirmStartingWeightCommand(Guid.NewGuid(), Guid.Empty, 60m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }

    [Fact]
    public void ZeroWeight_ShouldFail()
    {
        var command = new ConfirmStartingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 0, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }

    [Fact]
    public void NegativeWeight_ShouldFail()
    {
        var command = new ConfirmStartingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), -5m, WeightUnit.Kilograms);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }

    [Fact]
    public void InvalidUnit_ShouldFail()
    {
        var command = new ConfirmStartingWeightCommand(Guid.NewGuid(), Guid.NewGuid(), 60m, (WeightUnit)99);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Unit);
    }
}
