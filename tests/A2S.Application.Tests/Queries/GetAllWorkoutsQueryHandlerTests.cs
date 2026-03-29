using A2S.Application.Common;
using A2S.Application.Queries.GetAllWorkouts;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetAllWorkoutsQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetAllWorkoutsQueryHandler _handler;

    private static readonly UserId TestUserId = new(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"));

    public GetAllWorkoutsQueryHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new GetAllWorkoutsQueryHandler(_workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetAllWorkoutsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenNoWorkouts_ReturnsEmptyList()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetAllByUserSummaryAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workout>());

        var result = await _handler.Handle(new GetAllWorkoutsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenWorkoutsExist_ReturnsSummaryDtos()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetAllByUserSummaryAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Workout> { workout });

        var result = await _handler.Handle(new GetAllWorkoutsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Test");
        result.Value[0].ExerciseCount.Should().Be(1);
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
