using A2S.Application.Common;
using A2S.Application.Queries.SimulateWorkout;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class SimulateWorkoutQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly SimulateWorkoutQueryHandler _handler;

    private static readonly string TestUserId = "user_simulate_test";
    private static readonly Guid TestWorkoutId = Guid.Parse("aaaa1111-1111-1111-1111-111111111111");

    public SimulateWorkoutQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new SimulateWorkoutQueryHandler(_workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new SimulateWorkoutQuery(TestWorkoutId, 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsNotFoundFailure()
    {
        SetupAuthenticatedUser();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new SimulateWorkoutQuery(TestWorkoutId, 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenWorkoutBelongsToDifferentUser_ReturnsAccessDenied()
    {
        SetupAuthenticatedUser();
        var workout = new WorkoutBuilder()
            .WithUserId("user_other_person")
            .WithDefaultLinearExercise()
            .Build();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new SimulateWorkoutQuery(TestWorkoutId, 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("denied");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ReturnsSimulationResult()
    {
        SetupAuthenticatedUser();
        var workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, 100m)
            .Build();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new SimulateWorkoutQuery(TestWorkoutId, 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ExerciseTimeSeries.Should().HaveCount(1);
        result.Value.ExerciseTimeSeries[0].ExerciseName.Should().Be("Squat");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_PassesCorrectSessionCount()
    {
        SetupAuthenticatedUser();
        var workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithDefaultLinearExercise()
            .Build();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new SimulateWorkoutQuery(TestWorkoutId, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Initial point (0) + 3 simulated sessions = 4 data points
        result.Value.ExerciseTimeSeries[0].DataPoints.Should().HaveCount(4);
    }

    private void SetupAuthenticatedUser()
    {
        _currentUserService.UserId.Returns(TestUserId);
    }
}
