using A2S.Domain.Common;
using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Infrastructure.SeedData;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace A2S.Infrastructure.Tests.SeedData;

public class ExerciseLibraryProviderTests
{
    private readonly IExerciseDefinitionRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ExerciseLibraryProvider _provider;

    public ExerciseLibraryProviderTests()
    {
        _repository = Substitute.For<IExerciseDefinitionRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var definitions = new List<ExerciseDefinition>
        {
            new(new ExerciseDefinitionId(Guid.NewGuid()), "Squat (Barbell)", EquipmentType.Barbell, "Legs", true, "Barbell squat", 4, 6, 4),
            new(new ExerciseDefinitionId(Guid.NewGuid()), "Bench Press (Barbell)", EquipmentType.Barbell, "Chest", true, "Barbell bench press", 4, 6, 4),
            new(new ExerciseDefinitionId(Guid.NewGuid()), "Bicep Curl (Dumbbell)", EquipmentType.Dumbbell, "Arms", false, "DB curl", 8, 12, 3),
        };

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(definitions);

        _provider = new ExerciseLibraryProvider(_repository, _cache);
    }

    [Fact]
    public void AllTemplates_ReturnsExpectedExerciseCount()
    {
        _provider.AllTemplates.Should().HaveCount(3);
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
    public void AllTemplates_CachesResults()
    {
        var first = _provider.AllTemplates;
        var second = _provider.AllTemplates;

        _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
