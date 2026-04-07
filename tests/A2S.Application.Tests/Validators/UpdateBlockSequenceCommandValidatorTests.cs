using A2S.Application.Commands.UpdateBlockSequence;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class UpdateBlockSequenceCommandValidatorTests
{
    private readonly UpdateBlockSequenceCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new UpdateBlockSequenceCommand(
            Guid.Parse("fff11111-1111-1111-1111-111111111111"),
            [1, 2, 3]);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new UpdateBlockSequenceCommand(Guid.Empty, [1, 2, 3]);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyBlockSequence_ShouldFail()
    {
        var command = new UpdateBlockSequenceCommand(
            Guid.Parse("fff11111-1111-1111-1111-111111111111"),
            []);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BlockSequence);
    }

    [Fact]
    public void BlockValueTooLow_ShouldFail()
    {
        var command = new UpdateBlockSequenceCommand(
            Guid.Parse("fff11111-1111-1111-1111-111111111111"),
            [0, 1, 2]);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("BlockSequence[0]");
    }

    [Fact]
    public void BlockValueTooHigh_ShouldFail()
    {
        var command = new UpdateBlockSequenceCommand(
            Guid.Parse("fff11111-1111-1111-1111-111111111111"),
            [1, 2, 4]);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("BlockSequence[2]");
    }

    [Fact]
    public void AllBlockValuesInRange_ShouldPass()
    {
        var command = new UpdateBlockSequenceCommand(
            Guid.Parse("fff11111-1111-1111-1111-111111111111"),
            [1, 1, 2, 2, 3, 3, 1]);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
