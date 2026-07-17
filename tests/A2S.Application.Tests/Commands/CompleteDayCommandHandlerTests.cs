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

    [Fact]
    public async Task Handle_WhenRepsPerSetWeightIncreases_NextSessionPlanShowsNewWeight()
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

        // All sets hit max reps at max sets — SUCCESS bumps weight 20 -> 22.5
        var result = await _handler.Handle(
            CreateRepsPerSetCommand(workout, exercise.Id.Value, weight: 20m, reps: 12),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var next = result.Value.NextSessionExercises.Should().ContainSingle().Subject;
        next.ExerciseId.Should().Be(exercise.Id.Value);
        next.ExerciseName.Should().Be("Lateral Raise (Cable)");
        next.Weight.Should().Be(22.5m, "the preview must show the progressed weight, not the just-lifted one");
        next.WeightUnit.Should().Be("Kilograms");
        next.SetCount.Should().Be(5);
        next.TargetReps.Should().Be(12);
    }

    [Fact]
    public async Task Handle_WhenLinearExerciseMidWeek_NextSessionPlanUsesNextWeeksParameters()
    {
        SetupAuthenticatedUser(TestUserId);
        // FiveDay variant with only Day1 populated: completing Day1 does NOT advance
        // the week, but the next Day1 session is still next week's plan.
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
        workout.CurrentWeek.Should().Be(1, "other days of the week are still outstanding");

        // Expected plan for week 2, computed from post-progression state
        var expected = exercise
            .CalculatePlannedSets(workout.GetTemplateWeek(2), workout.GetBlockType(2))
            .ToList();

        var next = result.Value.NextSessionExercises.Should().ContainSingle().Subject;
        next.SetCount.Should().Be(expected.Count);
        next.TargetReps.Should().Be(4, "A2S week 2 primary-tier reps are 4, not week 1's 5");
        next.Weight.Should().Be(expected[0].Weight.Value);
        next.HasAmrap.Should().Be(expected.Any(s => s.IsAmrap));
    }

    [Fact]
    public async Task Handle_WhenCompletingDayInFinalWeek_NextSessionPlanIsEmpty()
    {
        SetupAuthenticatedUser(TestUserId);
        var builder = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FourDay)
            .WithBlockSequence([1]); // 7-week program
        foreach (var day in new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
        {
            builder.WithDefaultLinearExercise($"Lift {day}", day, 1, 100m);
        }
        var workout = builder.Build();
        workout.Start();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        // Complete weeks 1-6 to arrive at the final week
        for (var week = 1; week <= 6; week++)
        {
            foreach (var day in new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
            {
                var ex = workout.Exercises.Single(e => e.AssignedDay == day);
                var res = await _handler.Handle(
                    CreateLinearDayCommand(workout, ex.Id.Value, day),
                    CancellationToken.None);
                res.IsSuccess.Should().BeTrue($"week {week} {day} should complete");
            }
        }
        workout.CurrentWeek.Should().Be(7);

        var finalDayExercise = workout.Exercises.Single(e => e.AssignedDay == DayNumber.Day1);
        var result = await _handler.Handle(
            CreateLinearDayCommand(workout, finalDayExercise.Id.Value, DayNumber.Day1),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("completing a final-week day must not fail");
        result.Value.NextSessionExercises.Should().BeEmpty("there is no next session after the final week");
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
