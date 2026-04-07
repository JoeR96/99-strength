using A2S.Application.Commands.ProgressWeek;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class ProgressWeekCommandValidatorTests
{
    private readonly ProgressWeekCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new ProgressWeekCommand(Guid.Parse("aaa11111-1111-1111-1111-111111111111"));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyWorkoutId_ShouldFail()
    {
        var command = new ProgressWeekCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.WorkoutId);
    }
}
