using A2S.Application.Commands.ConfirmStartingWeight;
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

public class ConfirmStartingWeightCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ConfirmStartingWeightCommandHandler _handler;

    private static readonly UserId TestUserId = new(Guid.Parse("aaa66666-6666-6666-6666-666666666666"));

    public ConfirmStartingWeightCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new ConfirmStartingWeightCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenValid_ConfirmsWeightAndReturnsSuccess()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkoutWithRepsPerSet(TestUserId);
        var exerciseId = workout.Exercises.First().Id.Value;
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new ConfirmStartingWeightCommand(
            workout.Id.Value, exerciseId, 50m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new ConfirmStartingWeightCommand(
            Guid.NewGuid(), Guid.NewGuid(), 50m, WeightUnit.Kilograms);

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

        var command = new ConfirmStartingWeightCommand(
            Guid.NewGuid(), Guid.NewGuid(), 50m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkoutWithRepsPerSet(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new ConfirmStartingWeightCommand(
            workout.Id.Value, workout.Exercises.First().Id.Value, 50m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own");
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateWorkoutWithRepsPerSet(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithRepsPerSetProgression(
                "Lat Pulldown", ExerciseCategory.Accessory, EquipmentType.Cable,
                DayNumber.Day1, 1, "LAT001",
                RepRange.Create(8, 12), 3, 4, false,
                Weight.Create(50m, WeightUnit.Kilograms))
        };
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }
}
