using A2S.Application.Commands.UpdateWorkingWeight;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class UpdateWorkingWeightCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UpdateWorkingWeightCommandHandler _handler;

    private static readonly UserId TestUserId = new(Guid.Parse("aaa33333-3333-3333-3333-333333333333"));

    public UpdateWorkingWeightCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new UpdateWorkingWeightCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new UpdateWorkingWeightCommand(
            Guid.NewGuid(), Guid.NewGuid(), 60m, WeightUnit.Kilograms, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var command = new UpdateWorkingWeightCommand(
            Guid.NewGuid(), Guid.NewGuid(), 60m, WeightUnit.Kilograms, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new UpdateWorkingWeightCommand(
            workout.Id.Value, workout.Exercises.First().Id.Value, 60m, WeightUnit.Kilograms, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own");
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesWeightAndSaves()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        var exerciseId = workout.Exercises.First().Id.Value;
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new UpdateWorkingWeightCommand(
            workout.Id.Value, exerciseId, 60m, WeightUnit.Kilograms, "Felt easy");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateWorkout(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithRepsPerSetProgression(
                "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
                DayNumber.Day1, 1, "LAT001",
                RepRange.Create(8, 10, 12), 3, 4, false,
                Weight.Create(50m, WeightUnit.Kilograms))
        };
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }
}
