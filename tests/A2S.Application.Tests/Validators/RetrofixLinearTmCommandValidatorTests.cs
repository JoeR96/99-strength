using A2S.Application.Commands.RetrofixLinearTm;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class RetrofixLinearTmCommandValidatorTests
{
    private readonly RetrofixLinearTmCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new RetrofixLinearTmCommand(
            Guid.Parse("ccc11111-1111-1111-1111-111111111111"),
            Guid.Parse("ccc22222-2222-2222-2222-222222222222"),
            100m);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new RetrofixLinearTmCommand(Guid.Empty, Guid.Parse("ccc22222-2222-2222-2222-222222222222"), 100m);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }

    [Fact]
    public void EmptyExerciseId_ShouldFail()
    {
        var command = new RetrofixLinearTmCommand(Guid.Parse("ccc11111-1111-1111-1111-111111111111"), Guid.Empty, 100m);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExerciseId);
    }

    [Fact]
    public void ZeroOriginalStartingTm_ShouldFail()
    {
        var command = new RetrofixLinearTmCommand(
            Guid.Parse("ccc11111-1111-1111-1111-111111111111"),
            Guid.Parse("ccc22222-2222-2222-2222-222222222222"),
            0m);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OriginalStartingTm);
    }

    [Fact]
    public void NegativeOriginalStartingTm_ShouldFail()
    {
        var command = new RetrofixLinearTmCommand(
            Guid.Parse("ccc11111-1111-1111-1111-111111111111"),
            Guid.Parse("ccc22222-2222-2222-2222-222222222222"),
            -10m);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.OriginalStartingTm);
    }
}
