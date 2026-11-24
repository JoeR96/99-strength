using A2S.Application.Common;
using A2S.Application.Queries.GetWorkoutHistory;
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

public class GetWorkoutHistoryQueryHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetWorkoutHistoryQueryHandler _handler;

    private static readonly UserId TestUserId = new("ccc44444-4444-4444-4444-444444444444");
    private static readonly DateTime FixedDate = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);

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
        var otherUserId = new UserId(Guid.NewGuid().ToString());
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

    [Fact]
    public async Task History_ShowsCardioPerformance()
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
            new ExercisePerformance(stairs.Id, [], [], FixedDate,
                plannedCardio: stairs.Progression.CalculateCardioBlock(),
                cardio: new CardioResult(true, EffortRating.Easy, totalSeconds: 720, workMinutes: 7m))
        ]);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new GetWorkoutHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var week = result.Value!.ExerciseHistories.Single(h => h.ExerciseId == stairs.Id.Value).WeeklyHistory.Should().ContainSingle().Subject;
        week.CardioSummary.Should().Be("7 min @ level 6 · Easy");
        week.Sets.Should().BeEmpty();
        week.TotalVolume.Should().Be(0);
        result.Value.CompletedActivities.Single().Performances.Single().CardioSummary.Should().Be("7 min @ level 6 · Easy");
    }

    [Fact]
    public async Task History_ListsCardioSettingsChangesWithBeforeAndAfterText()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench").WithDay(DayNumber.Day1).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithDay(DayNumber.Day1).WithOrder(2).AsCardioStage(CardioLane.Easy))
            .Build();
        workout.Start();
        var walk = workout.Exercises.Single(e => e.Category == ExerciseCategory.Cardio);
        workout.SetCardioSettings(walk.Id, 5m, 5.6m);
        var audit = workout.AuditEntries.Single(a => a.Type == AuditEntryType.CardioSettingsChanged);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new GetWorkoutHistoryQuery(), CancellationToken.None);

        var history = result.Value!.ExerciseHistories.Single(h => h.ExerciseId == walk.Id.Value);
        var change = history.SettingsChanges.Should().ContainSingle().Subject;
        change.OldText.Should().Be(audit.OldValue);
        change.NewText.Should().Be(audit.NewValue);
        change.NewText.Should().Contain("5.6 km/h");
        change.WeekNumber.Should().Be(1);
        change.DayNumber.Should().Be(1);
        history.ProgressionChanges.Should().BeEmpty();
        result.Value.ExerciseHistories.Single(h => h.Name == "Bench").SettingsChanges.Should().BeEmpty();
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
