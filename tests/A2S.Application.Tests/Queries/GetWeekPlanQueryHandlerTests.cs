using A2S.Application.Common;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetWeekPlanQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IA2SProgramProvider _programProvider;
    private readonly GetWeekPlanQueryHandler _handler;

    private static readonly UserId TestUserId = new("ccc22222-2222-2222-2222-222222222222");

    public GetWeekPlanQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _programProvider = new A2SProgramProvider();
        _handler = new GetWeekPlanQueryHandler(_workoutRepository, _currentUserService, _programProvider);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(null, 1, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(null, 1, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenInvalidWeekNumber_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(null, 99, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Week number");
    }

    [Fact]
    public async Task Handle_WhenInvalidDayNumber_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(null, 1, 99), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Day number");
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsWeekPlanDto()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(null, 1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WeekNumber.Should().Be(1);
        result.Value.DayNumber.Should().Be(1);
        result.Value.Exercises.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithWorkoutId_LoadsSpecificWorkout()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new GetWeekPlanQuery(workout.Id.Value, 1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _workoutRepository.Received(1).GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>());
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateActiveWorkout(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "TEST123",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 3)
        };
        var workout = Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
        workout.Start();
        return workout;
    }
}
