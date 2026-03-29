using A2S.Application.Common;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetExerciseLibraryQueryHandlerTests
{
    private readonly IExerciseDefinitionRepository _repository;
    private readonly GetExerciseLibraryQueryHandler _handler;

    public GetExerciseLibraryQueryHandlerTests()
    {
        _repository = Substitute.For<IExerciseDefinitionRepository>();
        _handler = new GetExerciseLibraryQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllExercises()
    {
        var definitions = new List<ExerciseDefinition>
        {
            new(new ExerciseDefinitionId(Guid.NewGuid()), "Squat", EquipmentType.Barbell, "Legs", true, "Barbell squat"),
            new(new ExerciseDefinitionId(Guid.NewGuid()), "Bicep Curl", EquipmentType.Dumbbell, "Arms", false, "DB curl")
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(definitions);

        var result = await _handler.Handle(new GetExerciseLibraryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(2);
        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEquipmentTypeFilter_CallsSearchAsync()
    {
        _repository.SearchAsync(EquipmentType.Barbell, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ExerciseDefinition>());

        var query = new GetExerciseLibraryQuery { EquipmentType = EquipmentType.Barbell };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SearchAsync(
            EquipmentType.Barbell, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSearchTerm_CallsSearchAsync()
    {
        _repository.SearchAsync(null, null, "squat", Arg.Any<CancellationToken>())
            .Returns(new List<ExerciseDefinition>());

        var query = new GetExerciseLibraryQuery { SearchTerm = "squat" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SearchAsync(
            null, null, "squat", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        var definition = new ExerciseDefinition(
            new ExerciseDefinitionId(Guid.NewGuid()), "Bench Press", EquipmentType.Barbell,
            "Chest", true, "Flat barbell bench press", 5, 8, 10, 4);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ExerciseDefinition> { definition });

        var result = await _handler.Handle(new GetExerciseLibraryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var template = result.Value.Templates.First();
        template.Name.Should().Be("Bench Press");
        template.Equipment.Should().Be(EquipmentType.Barbell);
        template.DefaultRepRange.Should().NotBeNull();
        template.DefaultRepRange!.Minimum.Should().Be(5);
        template.DefaultRepRange.Target.Should().Be(8);
        template.DefaultRepRange.Maximum.Should().Be(10);
        template.DefaultSets.Should().Be(4);
    }
}
