using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Habits;

public class HabitDefinitionTests
{
    private static readonly UserId Joe = new("user_joe");

    [Fact]
    public void Create_Counter_KeepsTargetAndMinimum()
    {
        var water = HabitDefinition.Create(Joe, "Water (532ml bottle)", HabitType.Counter, 6, 4, "bottles", 0, icon: "droplet", externalKey: "terran:water");

        water.UserId.Should().Be(Joe);
        water.Name.Should().Be("Water (532ml bottle)");
        water.Type.Should().Be(HabitType.Counter);
        water.Target.Should().Be(6);
        water.Minimum.Should().Be(4);
        water.Unit.Should().Be("bottles");
        water.Order.Should().Be(0);
        water.Icon.Should().Be("droplet");
        water.ExternalKey.Should().Be("terran:water");
        water.Archived.Should().BeFalse();
        water.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_Checkbox_NormalisesTargetAndMinimumToOne()
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 5, 3, null, 4);

        creatine.Target.Should().Be(1);
        creatine.Minimum.Should().Be(1);
    }

    [Fact]
    public void Create_MinimumAboveTarget_Throws()
    {
        var act = () => HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 2, 3, null, 1);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*minimum*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveMinimum_Throws(int minimum)
    {
        var act = () => HabitDefinition.Create(Joe, "Walks", HabitType.Numeric, 2, minimum, null, 1);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_BlankName_Throws()
    {
        var act = () => HabitDefinition.Create(Joe, "  ", HabitType.Counter, 2, 1, null, 1);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SetTargets_AppliesSameRule()
    {
        var walks = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 1);

        walks.SetTargets(4, 2);
        var act = () => walks.SetTargets(1, 2);

        walks.Target.Should().Be(4);
        walks.Minimum.Should().Be(2);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Rename_SetOrder_Archive_Unarchive()
    {
        var habit = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 1);

        habit.Rename(" Daily walks ");
        habit.SetOrder(7);
        habit.Archive();
        var archived = habit.Archived;
        habit.Unarchive();

        habit.Name.Should().Be("Daily walks");
        habit.Order.Should().Be(7);
        archived.Should().BeTrue();
        habit.Archived.Should().BeFalse();
    }

    [Fact]
    public void Create_DefaultsStepToOne()
    {
        HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0).Step.Should().Be(1);
    }

    [Fact]
    public void Create_CounterWithStep_KeepsIt_AndSetStepChangesIt()
    {
        var protein = HabitDefinition.Create(Joe, "Protein", HabitType.Counter, 180, 140, "g", 0, icon: "meat", step: 25);
        protein.Step.Should().Be(25);

        protein.SetStep(10);

        protein.Step.Should().Be(10);
    }

    [Fact]
    public void Checkbox_AlwaysStepsByOne()
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 0, step: 5);

        creatine.Step.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Step_OutOfRange_Throws(int step)
    {
        var create = () => HabitDefinition.Create(Joe, "Protein", HabitType.Counter, 180, 140, "g", 0, step: step);

        create.Should().Throw<BusinessRuleViolationException>();
    }
}
