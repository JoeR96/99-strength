using A2S.Application.Commands.ProgressWeek;
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

public class ProgressWeekCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ProgressWeekCommandHandler _handler;

    private static readonly UserId TestUserId = new("aaa55555-5555-5555-5555-555555555555");

    public ProgressWeekCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new ProgressWeekCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new ProgressWeekCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var result = await _handler.Handle(
            new ProgressWeekCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid().ToString());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkoutWithAllDaysCompleted(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new ProgressWeekCommand(workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotActive_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId); // Not started = Draft status
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new ProgressWeekCommand(workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValid_ProgressesWeekAndReturnsResult()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkoutWithAllDaysCompleted(TestUserId);
        var expectedWeek = workout.CurrentWeek;
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var result = await _handler.Handle(
            new ProgressWeekCommand(workout.Id.Value), CancellationToken.None);

        // Domain auto-progresses week when all days are completed in CompleteDay,
        // so manual ProgressToNextWeek fails because no days are completed in the new week
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("days are completed");
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateWorkout(UserId userId)
    {
        var exercises = CreateFiveDayExercises();
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }

    private static Workout CreateActiveWorkoutWithAllDaysCompleted(UserId userId)
    {
        var workout = CreateWorkout(userId);
        workout.Start();
        CompleteAllDays(workout);
        return workout;
    }

    private static List<Exercise> CreateFiveDayExercises()
    {
        return new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "SQ001",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 3),
            Exercise.CreateWithLinearProgression(
                "Bench", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day2, 1, "BP001",
                TrainingMax.Create(80m, WeightUnit.Kilograms), true, 3),
            Exercise.CreateWithLinearProgression(
                "Deadlift", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day3, 1, "DL001",
                TrainingMax.Create(120m, WeightUnit.Kilograms), true, 3),
            Exercise.CreateWithLinearProgression(
                "OHP", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day4, 1, "OHP001",
                TrainingMax.Create(60m, WeightUnit.Kilograms), true, 3),
            Exercise.CreateWithLinearProgression(
                "Row", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day5, 1, "ROW001",
                TrainingMax.Create(70m, WeightUnit.Kilograms), true, 3)
        };
    }

    private static void CompleteAllDays(Workout workout)
    {
        var days = new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4, DayNumber.Day5 };
        foreach (var day in days)
        {
            var exercise = workout.Exercises.First(e => e.AssignedDay == day);
            var weight = Weight.Create(50m, WeightUnit.Kilograms);
            var plannedSets = new List<PlannedSet>
            {
                new(1, weight, 5),
                new(2, weight, 5),
                new(3, weight, 5)
            };
            var completedSets = new List<CompletedSet>
            {
                new(1, weight, 5),
                new(2, weight, 5),
                new(3, weight, 8, wasAmrap: true)
            };
            var performance = new ExercisePerformance(exercise.Id, plannedSets, completedSets);
            workout.CompleteDay(day, new[] { performance });
        }
    }
}
