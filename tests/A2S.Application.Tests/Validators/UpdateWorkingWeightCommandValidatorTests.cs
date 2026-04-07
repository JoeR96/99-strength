using A2S.Application.Commands.UpdateWorkingWeight;
using A2S.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class UpdateWorkingWeightCommandValidatorTests
{
    private readonly UpdateWorkingWeightCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Parse("aa011111-1111-1111-1111-111111111111"),
            Guid.Parse("aa022222-2222-2222-2222-222222222222"),
            50m,
            WeightUnit.Kilograms,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Empty,
            Guid.Parse("aa022222-2222-2222-2222-222222222222"),
            50m,
            WeightUnit.Kilograms,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyExerciseId_ShouldFail()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Parse("aa011111-1111-1111-1111-111111111111"),
            Guid.Empty,
            50m,
            WeightUnit.Kilograms,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }

    [Fact]
    public void NegativeWeight_ShouldFail()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Parse("aa011111-1111-1111-1111-111111111111"),
            Guid.Parse("aa022222-2222-2222-2222-222222222222"),
            -5m,
            WeightUnit.Kilograms,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewWeight);
    }

    [Fact]
    public void ZeroWeight_ShouldPass()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Parse("aa011111-1111-1111-1111-111111111111"),
            Guid.Parse("aa022222-2222-2222-2222-222222222222"),
            0m,
            WeightUnit.Kilograms,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.NewWeight);
    }

    [Fact]
    public void InvalidUnit_ShouldFail()
    {
        var command = new UpdateWorkingWeightCommand(
            Guid.Parse("aa011111-1111-1111-1111-111111111111"),
            Guid.Parse("aa022222-2222-2222-2222-222222222222"),
            50m,
            (WeightUnit)99,
            null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Unit);
    }
}
