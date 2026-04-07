using A2S.Application.Commands.CreateWorkout;
using A2S.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class CreateWorkoutCommandValidatorTests
{
    private readonly CreateWorkoutCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new CreateWorkoutCommand("My Workout", ProgramVariant.FiveDay);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldFail()
    {
        var command = new CreateWorkoutCommand("", ProgramVariant.FiveDay);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameExceeds200Characters_ShouldFail()
    {
        var longName = new string('A', 201);
        var command = new CreateWorkoutCommand(longName, ProgramVariant.FiveDay);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameExactly200Characters_ShouldPass()
    {
        var name = new string('A', 200);
        var command = new CreateWorkoutCommand(name, ProgramVariant.FiveDay);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void InvalidVariant_ShouldFail()
    {
        var command = new CreateWorkoutCommand("Test", (ProgramVariant)99);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Variant);
    }

    [Fact]
    public void ZeroTotalWeeks_ShouldFail()
    {
        var command = new CreateWorkoutCommand("Test", ProgramVariant.FiveDay, TotalWeeks: 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TotalWeeks);
    }

    [Fact]
    public void NegativeTotalWeeks_ShouldFail()
    {
        var command = new CreateWorkoutCommand("Test", ProgramVariant.FiveDay, TotalWeeks: -1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TotalWeeks);
    }

    [Fact]
    public void TotalWeeksExceeds52_ShouldFail()
    {
        var command = new CreateWorkoutCommand("Test", ProgramVariant.FiveDay, TotalWeeks: 53);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TotalWeeks);
    }

    [Fact]
    public void TotalWeeksExactly52_ShouldPass()
    {
        var command = new CreateWorkoutCommand("Test", ProgramVariant.FiveDay, TotalWeeks: 52);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.TotalWeeks);
    }
}
