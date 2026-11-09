using A2S.Domain.Entities;
using A2S.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Entities;

public class ExerciseDefinitionMuscleGroupTests
{
    private static ExerciseDefinition Squat() =>
        ExerciseDefinition.Create("Squat (Barbell)", EquipmentType.Barbell, "General", true, "Hevy: quadriceps");

    [Fact]
    public void Create_StartsWithNoSecondaryMuscleGroups()
    {
        Squat().SecondaryMuscleGroups.Should().BeEmpty();
    }

    [Fact]
    public void SetMuscleGroups_NormalisesPrimaryAndSecondary()
    {
        var definition = Squat();

        definition.SetMuscleGroups(" Quadriceps ", [" Glutes", "HAMSTRINGS ", "glutes", "", "  "]);

        definition.MuscleGroup.Should().Be("quadriceps");
        definition.SecondaryMuscleGroups.Should().Equal("glutes", "hamstrings");
    }

    [Fact]
    public void SetMuscleGroups_ExcludesPrimaryFromSecondary()
    {
        var definition = Squat();

        definition.SetMuscleGroups("quadriceps", ["quadriceps", "glutes"]);

        definition.SecondaryMuscleGroups.Should().Equal("glutes");
    }

    [Fact]
    public void SetMuscleGroups_ReplacesEarlierSecondaryGroups()
    {
        var definition = Squat();
        definition.SetMuscleGroups("quadriceps", ["glutes", "hamstrings"]);

        definition.SetMuscleGroups("quadriceps", ["adductors"]);

        definition.SecondaryMuscleGroups.Should().Equal("adductors");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetMuscleGroups_RejectsEmptyPrimary(string primary)
    {
        var definition = Squat();

        var act = () => definition.SetMuscleGroups(primary, []);

        act.Should().Throw<Exception>().WithMessage("*Primary muscle group*");
    }
}
