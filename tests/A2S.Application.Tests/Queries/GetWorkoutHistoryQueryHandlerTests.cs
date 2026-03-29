using A2S.Application.Common;
using A2S.Application.Queries.GetWorkoutHistory;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetWorkoutHistoryQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetWorkoutHistoryQueryHandler _handler;

    private static readonly UserId TestUserId = new(Guid.Parse("ccc44444-4444-4444-4444-444444444444"));

    public GetWorkoutHistoryQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new GetWorkoutHistoryQueryHandler(_workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new GetWorkoutHistoryQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenNoActiveWorkout_ReturnsNull()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new GetWorkoutHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithWorkoutId_LoadsSpecificWorkout()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        var workoutId = workout.Id.Value;
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWorkoutHistoryQuery(workoutId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.WorkoutName.Should().Be("Test");
    }

    [Fact]
    public async Task Handle_WithWorkoutIdNotOwnedByUser_ReturnsFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWorkoutHistoryQuery(workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithoutWorkoutId_LoadsActiveWorkout()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        workout.Start();
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWorkoutHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
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
