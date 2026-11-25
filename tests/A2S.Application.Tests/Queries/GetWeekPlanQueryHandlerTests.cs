using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
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

    [Fact]
    public async Task WeekPlan_CardioExercise_HasBlockAndNoSets()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench").WithDay(DayNumber.Day1).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithDay(DayNumber.Day1).WithOrder(2).AsCardioStage(CardioLane.Hard))
            .Build();
        workout.Start();
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new GetWeekPlanQuery(null, 1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Exercises[0].CardioBlock.Should().BeNull();
        var stairs = result.Value.Exercises[1];
        stairs.Name.Should().Be("Stair Machine (Floors)");
        stairs.Category.Should().Be("Cardio");
        stairs.ProgressionType.Should().Be("CardioStage");
        stairs.PlannedSets.Should().BeEmpty();
        stairs.RestSeconds.Should().Be(0);
        stairs.Metadata.IsCardioSettingsPending.Should().BeFalse();
        stairs.CardioBlock.Should().BeEquivalentTo(new PlannedCardioBlockDto(
            Lane: "Hard",
            StageName: "Continuous",
            StageNumber: 1,
            StageCount: 3,
            WarmUp: new TrackSegmentDto(3, 3m, null),
            CoolDown: new TrackSegmentDto(2, 3m, null),
            Kind: "Continuous",
            WorkMinutes: 6,
            Intervals: null,
            RecoveryLevel: null,
            IntensityUnit: "Level",
            Intensity: 6m,
            SuggestedIntensity: null,
            Speed: null,
            IsSettingsPending: false,
            TotalMinutes: 11,
            Text: "3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)"));
    }

    [Fact]
    public async Task WeekPlan_WalkWithoutIncline_IsSettingsPending()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Squat").WithDay(DayNumber.Day2).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithDay(DayNumber.Day2).WithOrder(2).AsCardioStage(CardioLane.Easy))
            .Build();
        workout.Start();
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new GetWeekPlanQuery(null, 1, 2), CancellationToken.None);

        var walk = result.Value.Exercises[1];
        walk.Metadata.IsCardioSettingsPending.Should().BeTrue();
        walk.CardioBlock!.IsSettingsPending.Should().BeTrue();
        walk.CardioBlock.WarmUp.Should().BeNull();
        walk.CardioBlock.IntensityUnit.Should().Be("InclinePercent");
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
