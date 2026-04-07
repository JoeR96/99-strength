using A2S.Application.Commands.UpdateExercises;
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

public class UpdateExercisesCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UpdateExercisesCommandHandler _handler;

    private static readonly UserId TestUserId = new("a1d11111-1111-1111-1111-111111111111");

    public UpdateExercisesCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new UpdateExercisesCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new UpdateExercisesCommand(
            Guid.Parse("a1d22222-2222-2222-2222-222222222222"),
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = Guid.Parse("a1d33333-3333-3333-3333-333333333333") }
            });

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

        var command = new UpdateExercisesCommand(
            Guid.Parse("a1d22222-2222-2222-2222-222222222222"),
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = Guid.Parse("a1d33333-3333-3333-3333-333333333333") }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId("a1d44444-4444-4444-4444-444444444444");
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = workout.Exercises.First().Id.Value, TrainingMaxValue = 110m }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own workouts");
    }

    [Fact]
    public async Task Handle_WhenExerciseNotFound_ReturnsPartialResult()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = Guid.Parse("a1d55555-5555-5555-5555-555555555555"), TrainingMaxValue = 110m }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(0);
        result.Value.Results.Should().ContainSingle()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTrainingMaxUpdate_UpdatesSuccessfully()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new()
                {
                    ExerciseId = exercise.Id.Value,
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms,
                    Reason = "Adjusted up"
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(1);
        result.Value.Results.Should().ContainSingle()
            .Which.Success.Should().BeTrue();
        _workoutRepository.Received(1).Update(Arg.Any<Workout>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTrainingMaxUpdateOnNonLinear_ReturnsFailureForThatExercise()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FiveDay)
            .WithExercise(e => e
                .WithName("Face Pulls")
                .WithDay(DayNumber.Day1)
                .AsRepsPerSet(8, 12, 2, 4))
            .Build();
        workout.Start();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new()
                {
                    ExerciseId = exercise.Id.Value,
                    TrainingMaxValue = 110m
                }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(0);
        result.Value.Results.Should().ContainSingle()
            .Which.Message.Should().Contain("does not support Training Max");
    }

    [Fact]
    public async Task Handle_WhenBatchUpdate_UpdatesMultipleExercises()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, 100m)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day2, 1, 80m)
            .Build();
        workout.Start();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercises = workout.Exercises.ToList();
        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = exercises[0].Id.Value, TrainingMaxValue = 110m },
                new() { ExerciseId = exercises[1].Id.Value, TrainingMaxValue = 85m }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenNoApplicableUpdates_ReturnsZeroUpdated()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new UpdateExercisesCommand(
            workout.Id.Value,
            new List<ExerciseUpdateRequest>
            {
                new() { ExerciseId = exercise.Id.Value }
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(0);
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
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
}
