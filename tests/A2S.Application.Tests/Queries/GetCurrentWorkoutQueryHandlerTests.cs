using A2S.Application.Common;
using A2S.Application.Queries.GetWorkout;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetCurrentWorkoutQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetCurrentWorkoutQueryHandler _handler;

    private static readonly UserId TestUserId = new(Guid.Parse("ccc11111-1111-1111-1111-111111111111"));

    public GetCurrentWorkoutQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new GetCurrentWorkoutQueryHandler(_workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetCurrentWorkoutQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenNoActiveWorkout_ReturnsSuccessWithNull()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(new GetCurrentWorkoutQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenActiveWorkoutExists_ReturnsWorkoutDto()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(new GetCurrentWorkoutQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test");
        result.Value.Exercises.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_MapsExerciseProgressionDataCorrectly()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(new GetCurrentWorkoutQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var exercise = result.Value!.Exercises.First();
        exercise.Name.Should().Be("Squat");
        exercise.Progression.Should().NotBeNull();
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
