using A2S.Application.Commands.SetActiveWorkout;
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

public class SetActiveWorkoutCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly SetActiveWorkoutCommandHandler _handler;

    private static readonly UserId TestUserId = new("aaa11111-1111-1111-1111-111111111111");

    public SetActiveWorkoutCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new SetActiveWorkoutCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenValid_ActivatesWorkoutAndReturnsSuccess()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new SetActiveWorkoutCommand(workout.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnotherWorkoutActive_DeactivatesItFirst()
    {
        SetupAuthenticatedUser(TestUserId);
        var currentActive = CreateWorkout(TestUserId);
        currentActive.Start();
        var newWorkout = CreateWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(newWorkout);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(currentActive);

        var result = await _handler.Handle(
            new SetActiveWorkoutCommand(newWorkout.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _workoutRepository.Received(2).Update(Arg.Any<Workout>());
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new SetActiveWorkoutCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new SetActiveWorkoutCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid().ToString());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new SetActiveWorkoutCommand(workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own");
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateWorkout(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "TEST123",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 3)
        };
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }
}
