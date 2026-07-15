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

    [Fact]
    public async Task Handle_WhenPendingWorkingWeight_AutoConfirmsFromActualLiftedWeight()
    {
        SetupAuthenticatedUser(TestUserId);
        var builder = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FourDay)
            .WithExercise(b => b
                .WithName("Lateral Raise (Cable)")
                .WithCategory(ExerciseCategory.Accessory)
                .WithEquipment(EquipmentType.Cable)
                .WithDay(DayNumber.Day1)
                .WithOrder(1)
                .AsRepsPerSet(startingSets: 5, targetSets: 5,
                    startingWeight: Weight.Create(20m, WeightUnit.Kilograms)));
        foreach (var day in new[] { DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
        {
            builder.WithDefaultLinearExercise($"Filler {day}", day, 1, 100m);
        }
        var workout = builder.Build();
        workout.Start();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.Single(e => e.AssignedDay == DayNumber.Day1);

        // Week 1: all sets hit max reps at max sets — weight bumps to 22.5kg and awaits confirmation
        var week1 = await _handler.Handle(
            CreateRepsPerSetCommand(workout, exercise.Id.Value, weight: 20m, reps: 12),
            CancellationToken.None);

        week1.IsSuccess.Should().BeTrue();
        week1.Value.ExercisesPendingWeightConfirmation.Should().ContainSingle(
            p => p.ConfirmationType == ConfirmationType.WorkingWeight);
        exercise.Progression.PendingWeightConfirmation.Should().BeTrue();
        exercise.Progression.GetCurrentWeight()!.Value.Should().Be(22.5m);

        // Complete the remaining days so week 2 starts
        foreach (var day in new[] { DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
        {
            var filler = workout.Exercises.Single(e => e.AssignedDay == day);
            var fillerResult = await _handler.Handle(
                CreateLinearDayCommand(workout, filler.Id.Value, day),
                CancellationToken.None);
            fillerResult.IsSuccess.Should().BeTrue();
        }

        // Week 2: the gym stack has no 22.5 so the user lifts 25. The actual lifted
        // weight becomes the confirmed working weight without any explicit prompt.
        var week2 = await _handler.Handle(
            CreateRepsPerSetCommand(workout, exercise.Id.Value, weight: 25m, reps: 10),
            CancellationToken.None);

        week2.IsSuccess.Should().BeTrue();
        exercise.Progression.PendingWeightConfirmation.Should().BeFalse(
            "the weight logged at the next session confirms the stack weight");
        exercise.Progression.GetCurrentWeight()!.Value.Should().Be(25m);
    }

    private static CompleteDayCommand CreateLinearDayCommand(Workout workout, Guid exerciseId, DayNumber day)
    {
        return new CompleteDayCommand(
            workout.Id.Value,
            day,
            new List<ExercisePerformanceRequest>
            {
                new()
                {
                    ExerciseId = exerciseId,
                    CompletedSets = new List<CompletedSetRequest>
                    {
                        new() { SetNumber = 1, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = 5, WasAmrap = false }
                    }
                }
            });
    }

    private static CompleteDayCommand CreateRepsPerSetCommand(
        Workout workout, Guid exerciseId, decimal weight, int reps)
    {
        var sets = Enumerable.Range(1, 5)
            .Select(n => new CompletedSetRequest
            {
                SetNumber = n,
                Weight = weight,
                WeightUnit = WeightUnit.Kilograms,
                ActualReps = reps,
                WasAmrap = false
            })
            .ToList();

        return new CompleteDayCommand(
            workout.Id.Value,
            DayNumber.Day1,
            new List<ExercisePerformanceRequest> { new() { ExerciseId = exerciseId, CompletedSets = sets } });
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
