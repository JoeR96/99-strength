using A2S.Domain.Enums;
using A2S.Infrastructure.SeedData;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests;

public class ExerciseLibraryProviderTests
{
    private readonly ExerciseLibraryProvider _provider = new();

    [Fact]
    public void AllTemplates_ReturnsExpectedExerciseCount()
    {
        _provider.AllTemplates.Should().HaveCountGreaterThan(400);
    }

    [Fact]
    public void AllTemplates_AllHaveNames()
    {
        _provider.AllTemplates.Should().AllSatisfy(t =>
            t.Name.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void AllTemplates_AllHaveValidEquipmentType()
    {
        _provider.AllTemplates.Should().AllSatisfy(t =>
            Enum.IsDefined(t.Equipment).Should().BeTrue());
    }

    [Fact]
    public void AllTemplates_AllHaveDefaultRepRange()
    {
        _provider.AllTemplates.Should().AllSatisfy(t =>
            t.DefaultRepRange.Should().NotBeNull());
    }

    [Fact]
    public void GetByName_ExistingExercise_ReturnsTemplate()
    {
        var template = _provider.GetByName("Squat (Barbell)");
        template.Should().NotBeNull();
        template!.Equipment.Should().Be(EquipmentType.Barbell);
    }

    [Fact]
    public void GetByName_CaseInsensitive_ReturnsTemplate()
    {
        var template = _provider.GetByName("squat (barbell)");
        template.Should().NotBeNull();
        template!.Name.Should().Be("Squat (Barbell)");
    }

    [Fact]
    public void GetByName_NonExistent_ReturnsNull()
    {
        _provider.GetByName("NonExistent Exercise").Should().BeNull();
    }

    [Fact]
    public void SmithMachineExercises_HaveCorrectEquipmentType()
    {
        var smithExercises = _provider.AllTemplates
            .Where(t => t.Name.Contains("Smith", StringComparison.OrdinalIgnoreCase))
            .ToList();

        smithExercises.Should().NotBeEmpty();
        smithExercises.Should().AllSatisfy(t =>
            t.Equipment.Should().Be(EquipmentType.SmithMachine));
    }

    [Fact]
    public void AllTemplates_IsSameInstanceOnMultipleCalls()
    {
        // Verify lazy loading returns same instance
        var first = _provider.AllTemplates;
        var second = _provider.AllTemplates;
        ReferenceEquals(first, second).Should().BeTrue();
    }
}
