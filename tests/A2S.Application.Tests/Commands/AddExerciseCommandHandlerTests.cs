using A2S.Application.Commands.AddExercise;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class AddExerciseCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IExerciseLibraryProvider _library = Substitute.For<IExerciseLibraryProvider>();
    private readonly AddExerciseCommandHandler _handler;

    private static readonly UserId TestUserId = new("aaa88888-8888-8888-8888-888888888888");
    private static readonly Guid TestWorkoutGuid = Guid.Parse("bbb88888-8888-8888-8888-888888888888");

    public AddExerciseCommandHandlerTests()
    {
        _handler = new AddExerciseCommandHandler(_workoutRepository, _unitOfWork, _currentUserService, _library);
        _library.GetByName("Bicep Curl (Dumbbell)").Returns(new ExerciseTemplate("Bicep Curl (Dumbbell)", EquipmentType.Dumbbell));
        _library.GetByName("Stair Machine (Floors)").Returns(new ExerciseTemplate("Stair Machine (Floors)", EquipmentType.Machine));
    }

    private static CreateExerciseRequest Curl(int order = 3, decimal? weight = 14m) => new()
    {
        TemplateName = "Bicep Curl (Dumbbell)",
        ExternalTemplateId = "37FCC2BB",
        Category = ExerciseCategory.Accessory,
        ProgressionType = "RepsPerSet",
        AssignedDay = DayNumber.Day1,
        OrderInDay = order,
        StartingWeight = weight,
        WeightUnit = weight is null ? null : WeightUnit.Kilograms,
        StartingSets = 2,
        TargetSets = 4,
        RepRangeMinimum = 10,
        RepRangeMaximum = 12
    };

    [Fact]
    public async Task Handle_InsertsBeforeLaterExercises_WithStartingWeight_AndSaves()
    {
        _currentUserService.UserId.Returns(TestUserId.Value.ToString());
        var workout = UpperA(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new AddExerciseCommand(TestWorkoutGuid, Curl()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEquivalentTo(new { Name = "Bicep Curl (Dumbbell)", AssignedDay = 1, OrderInDay = 3 });
        workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).OrderBy(e => e.OrderInDay).Select(e => e.Name)
            .Should().Equal("Bench", "Skullcrusher", "Bicep Curl (Dumbbell)", "Pallof");
        var curl = workout.Exercises.Single(e => e.Name == "Bicep Curl (Dumbbell)");
        curl.ExternalTemplateId.Should().Be("37FCC2BB");
        curl.Progression.Should().BeOfType<RepsPerSetStrategy>()
            .Which.CurrentWeight!.Value.Should().Be(14m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownTemplate_FailsWithoutSaving()
    {
        _currentUserService.UserId.Returns(TestUserId.Value.ToString());
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(UpperA(TestUserId));

        var result = await _handler.Handle(
            new AddExerciseCommand(TestWorkoutGuid, Curl() with { TemplateName = "Nope" }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found in the exercise library");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new AddExerciseCommand(TestWorkoutGuid, Curl()), CancellationToken.None);

        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        _currentUserService.UserId.Returns(TestUserId.Value.ToString());
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(UpperA(new UserId(Guid.NewGuid().ToString())));

        var result = await _handler.Handle(new AddExerciseCommand(TestWorkoutGuid, Curl()), CancellationToken.None);

        result.Error.Should().Contain("own");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsInvertedRepRange_AndWeightWithoutUnit()
    {
        var validator = new AddExerciseCommandValidator();

        validator.Validate(new AddExerciseCommand(TestWorkoutGuid, Curl())).IsValid.Should().BeTrue();
        validator.Validate(new AddExerciseCommand(TestWorkoutGuid, Curl() with { RepRangeMinimum = 13 })).IsValid.Should().BeFalse();
        validator.Validate(new AddExerciseCommand(TestWorkoutGuid, Curl() with { WeightUnit = null })).IsValid.Should().BeFalse();
    }

    private static CreateExerciseRequest Stairs(CardioLane? lane = CardioLane.Hard) => new()
    {
        TemplateName = "Stair Machine (Floors)",
        ExternalTemplateId = "4377A52B",
        Category = ExerciseCategory.Cardio,
        ProgressionType = "CardioStage",
        AssignedDay = DayNumber.Day1,
        OrderInDay = 4,
        CardioLane = lane
    };

    [Fact]
    public async Task AddExercise_CardioStage_IsAccepted()
    {
        _currentUserService.UserId.Returns(TestUserId.Value.ToString());
        var workout = UpperA(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(workout);

        new AddExerciseCommandValidator().Validate(new AddExerciseCommand(TestWorkoutGuid, Stairs())).IsValid.Should().BeTrue();
        var result = await _handler.Handle(new AddExerciseCommand(TestWorkoutGuid, Stairs()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var stairs = workout.Exercises.Single(e => e.Name == "Stair Machine (Floors)");
        stairs.Category.Should().Be(ExerciseCategory.Cardio);
        stairs.Equipment.Should().Be(EquipmentType.Machine);
        stairs.OrderInDay.Should().Be(4);
        stairs.ExternalTemplateId.Should().Be("4377A52B");
        stairs.Progression.Should().BeOfType<CardioStageStrategy>().Which.Lane.Should().Be(CardioLane.Hard);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsCardioStageWithoutALane()
    {
        new AddExerciseCommandValidator().Validate(new AddExerciseCommand(TestWorkoutGuid, Stairs(lane: null))).IsValid.Should().BeFalse();
    }

    private static Workout UpperA(UserId userId) => Workout.Create(userId, "Test", ProgramVariant.FourDay,
    [
        Exercise.CreateWithLinearProgression("Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "B1", TrainingMax.Create(100m, WeightUnit.Kilograms), true, 3),
        Exercise.CreateWithRepsPerSetProgression("Skullcrusher", ExerciseCategory.Accessory, EquipmentType.Dumbbell,
            DayNumber.Day1, 2, "S1", RepRange.Create(10, 12)),
        Exercise.CreateWithRepsPerSetProgression("Pallof", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 3, "P1", RepRange.Create(10, 10))
    ]);
}
