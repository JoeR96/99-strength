using A2S.Application.Common;
using A2S.Application.Queries.GetExerciseHistory;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
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

    [Fact]
    public async Task Handle_CardioExercise_KeepsSessionsWithASummary()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench").WithDay(DayNumber.Day1).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithDay(DayNumber.Day1).WithOrder(2).AsCardioStage(CardioLane.Hard))
            .Build();
        workout.Start();
        var stairs = workout.Exercises.Single(e => e.Category == ExerciseCategory.Cardio);
        workout.CompleteDay(DayNumber.Day1,
        [
            new ExercisePerformance(stairs.Id, [], [], new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc),
                plannedCardio: stairs.Progression.CalculateCardioBlock(),
                cardio: new CardioResult(true, EffortRating.Easy, workMinutes: 7m))
        ]);
        _workoutRepository.GetAllAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(new List<Workout> { workout });

        var result = await _handler.Handle(new GetExerciseHistoryQuery("Stair Machine (Floors)"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value!.TotalSessions.Should().Be(1);
        result.Value.TotalVolume.Should().Be(0);
        result.Value.PersonalRecordWeight.Should().Be(0);
        var session = result.Value.Sessions.Should().ContainSingle().Subject;
        session.Sets.Should().BeEmpty();
        session.CardioSummary.Should().Be("7 min @ level 6 · Easy");
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
