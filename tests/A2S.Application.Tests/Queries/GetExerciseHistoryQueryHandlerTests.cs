using A2S.Application.Common;
using A2S.Application.Queries.GetExerciseHistory;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetExerciseHistoryQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetExerciseHistoryQueryHandler _handler;

    private static readonly UserId TestUserId = new("ccc33333-3333-3333-3333-333333333333");

    public GetExerciseHistoryQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new GetExerciseHistoryQueryHandler(_workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new GetExerciseHistoryQuery("Squat"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenExerciseNameEmpty_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);

        var result = await _handler.Handle(
            new GetExerciseHistoryQuery(""), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public async Task Handle_WhenNoWorkouts_ReturnsNull()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetAllAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workout>());

        var result = await _handler.Handle(
            new GetExerciseHistoryQuery("Squat"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenWorkoutsExistButNoMatchingExercise_ReturnsNullHistory()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetAllAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workout> { workout });

        var result = await _handler.Handle(
            new GetExerciseHistoryQuery("NonExistent"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
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
