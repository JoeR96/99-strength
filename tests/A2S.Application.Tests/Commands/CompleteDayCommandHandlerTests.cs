using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class CompleteDayCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly CompleteDayCommandHandler _handler;

    private static readonly UserId TestUserId = new("cdc11111-1111-1111-1111-111111111111");
    private static readonly Guid TestWorkoutGuid = Guid.Parse("cdc22222-2222-2222-2222-222222222222");

    public CompleteDayCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new CompleteDayCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = CreateValidCommand();
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var command = CreateValidCommand();
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId("cdc33333-3333-3333-3333-333333333333");
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = CreateValidCommand(workout);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own workouts");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotActive_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateDraftWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = CreateValidCommand(workout);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public async Task Handle_WhenNoExercisesForDay_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new CompleteDayCommand(
            workout.Id.Value,
            DayNumber.Day5,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = Guid.Parse("cdc44444-4444-4444-4444-444444444444"),
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5 }
                    }
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No exercises assigned");
    }

    [Fact]
    public async Task Handle_WhenExerciseNotAssignedToDay_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var wrongExerciseId = Guid.Parse("cdc55555-5555-5555-5555-555555555555");
        var command = new CompleteDayCommand(
            workout.Id.Value,
            DayNumber.Day1,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = wrongExerciseId,
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5 }
                    }
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found or not assigned");
    }

    [Fact]
    public async Task Handle_WhenValid_CompletesDay()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var command = new CompleteDayCommand(
            workout.Id.Value,
            DayNumber.Day1,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = exercise.Id.Value,
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = false },
                        new() { SetNumber = 2, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = false },
                        new() { SetNumber = 3, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = false },
                        new() { SetNumber = 4, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = false },
                        new() { SetNumber = 5, Weight = 79m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = true }
                    }
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Day.Should().Be(DayNumber.Day1);
        result.Value.ExercisesCompleted.Should().Be(1);
        _workoutRepository.Received(1).Update(Arg.Any<Workout>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTemporarySubstitution_SkipsProgression()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var command = new CompleteDayCommand(
            workout.Id.Value,
            DayNumber.Day1,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = exercise.Id.Value,
                    WasTemporarySubstitution = true,
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 8, WasAmrap = false },
                        new() { SetNumber = 2, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 8, WasAmrap = false },
                        new() { SetNumber = 3, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 8, WasAmrap = false },
                        new() { SetNumber = 4, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 8, WasAmrap = false },
                        new() { SetNumber = 5, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 8, WasAmrap = true }
                    }
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProgressionChanges.Should().ContainSingle()
            .Which.Change.Should().Contain("Skipped");
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static CompleteDayCommand CreateValidCommand(Workout? workout = null)
    {
        var workoutId = workout?.Id.Value ?? TestWorkoutGuid;
        return new CompleteDayCommand(
            workoutId,
            DayNumber.Day1,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = Guid.Parse("cdc44444-4444-4444-4444-444444444444"),
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 100m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5 }
                    }
                }
            });
    }

    private static Workout CreateActiveWorkout(UserId userId)
    {
        var workout = new WorkoutBuilder()
            .WithUserId(userId)
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();
        workout.Start();
        return workout;
    }

    private static Workout CreateDraftWorkout(UserId userId)
    {
        return new WorkoutBuilder()
            .WithUserId(userId)
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();
    }
}
