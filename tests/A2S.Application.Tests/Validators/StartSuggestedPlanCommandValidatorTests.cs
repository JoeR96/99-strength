using A2S.Application.Commands.SuggestedPlans;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Validators;

public class StartSuggestedPlanCommandValidatorTests
{
    private readonly StartSuggestedPlanCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_ShouldPassValidation()
    {
        var result = _validator.TestValidate(new StartSuggestedPlanCommand(100m, 140m, 60m, 180m, 95m));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DefaultStartWeight_IsNinetyFive()
    {
        new StartSuggestedPlanCommand(100m, 140m, 60m, 180m).StartWeightKg.Should().Be(95m);
    }

    [Theory]
    [InlineData(19.5)]
    [InlineData(0)]
    [InlineData(400.5)]
    public void TrainingMaxOutsideTwentyToFourHundred_ShouldFail(decimal tm)
    {
        _validator.TestValidate(new StartSuggestedPlanCommand(tm, 140m, 60m, 180m)).ShouldHaveValidationErrorFor(x => x.BenchTm);
        _validator.TestValidate(new StartSuggestedPlanCommand(100m, tm, 60m, 180m)).ShouldHaveValidationErrorFor(x => x.SquatTm);
        _validator.TestValidate(new StartSuggestedPlanCommand(100m, 140m, tm, 180m)).ShouldHaveValidationErrorFor(x => x.OhpTm);
        _validator.TestValidate(new StartSuggestedPlanCommand(100m, 140m, 60m, tm)).ShouldHaveValidationErrorFor(x => x.DeadliftTm);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(400)]
    public void TrainingMaxOnTheBoundary_ShouldPass(decimal tm)
    {
        _validator.TestValidate(new StartSuggestedPlanCommand(tm, tm, tm, tm)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(29.9)]
    [InlineData(300.1)]
    public void StartWeightOutsideThirtyToThreeHundred_ShouldFail(decimal kg)
    {
        _validator.TestValidate(new StartSuggestedPlanCommand(100m, 140m, 60m, 180m, kg)).ShouldHaveValidationErrorFor(x => x.StartWeightKg);
    }
}
