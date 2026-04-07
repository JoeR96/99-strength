using A2S.Application.Commands.UpdateBlockSequence;
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

public class UpdateBlockSequenceCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UpdateBlockSequenceCommandHandler _handler;

    private static readonly UserId TestUserId = new("aaa88888-8888-8888-8888-888888888888");

    public UpdateBlockSequenceCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new UpdateBlockSequenceCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesBlockSequenceAndReturnsResult()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var newSequence = new List<int> { 1, 1, 2, 3 };
        var command = new UpdateBlockSequenceCommand(workout.Id.Value, newSequence);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BlockSequence.Should().BeEquivalentTo(newSequence);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new UpdateBlockSequenceCommand(Guid.NewGuid(), new List<int> { 1, 2, 3 });

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

        var command = new UpdateBlockSequenceCommand(Guid.NewGuid(), new List<int> { 1, 2, 3 });

        var result = await _handler.Handle(command, CancellationToken.None);

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

        var command = new UpdateBlockSequenceCommand(workout.Id.Value, new List<int> { 1, 2, 3 });

        var result = await _handler.Handle(command, CancellationToken.None);

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
