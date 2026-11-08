using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class SubstituteExerciseCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExerciseLibraryProvider _library;
    private readonly SubstituteExerciseCommandHandler _handler;

    private static readonly UserId TestUserId = new("a0b11111-1111-1111-1111-111111111111");

    public SubstituteExerciseCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _library = Substitute.For<IExerciseLibraryProvider>();
        _library.GetAllTemplatesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ExerciseTemplate>());
        var budget = new DayBudgetCalculator(new VolumeBudgetService(new A2SProgramProvider()), _library);
        _handler = new SubstituteExerciseCommandHandler(_workoutRepository, _unitOfWork, _currentUserService, budget);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new SubstituteExerciseCommand(
            Guid.Parse("a0b22222-2222-2222-2222-222222222222"),
            Guid.Parse("a0b33333-3333-3333-3333-333333333333"),
            "New Exercise");

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

        var command = new SubstituteExerciseCommand(
            Guid.Parse("a0b22222-2222-2222-2222-222222222222"),
            Guid.Parse("a0b33333-3333-3333-3333-333333333333"),
            "New Exercise");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenNotOwner_ReturnsFailure()
    {
        var otherUserId = new UserId("a0b44444-4444-4444-4444-444444444444");
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new SubstituteExerciseCommand(
            workout.Id.Value, exercise.Id.Value, "New Exercise");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("own workouts");
    }

    [Fact]
    public async Task Handle_WhenExerciseNotFound_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new SubstituteExerciseCommand(
            workout.Id.Value,
            Guid.Parse("a0b55555-5555-5555-5555-555555555555"),
            "New Exercise");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenValidBasicSubstitution_ReturnsSuccess()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new SubstituteExerciseCommand(
            workout.Id.Value,
            exercise.Id.Value,
            "Front Squat",
            "front-squat-barbell",
            "Prefer front squat");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewName.Should().Be("Front Squat");
        result.Value.ProgressionTypeChanged.Should().BeFalse();
        _workoutRepository.Received(1).Update(Arg.Any<Workout>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSubstitutionWithProgressionChange_ReturnsSuccess()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new SubstituteExerciseCommand(
            workout.Id.Value,
            exercise.Id.Value,
            "Cable Row",
            "cable-row",
            "Switching to cable",
            new ProgressionConfigDto
            {
                Type = "RepsPerSet",
                RepRangeMinimum = 8,
                RepRangeMaximum = 12,
                StartingSets = 2,
                TargetSets = 4,
                StartingWeight = 50m,
                WeightUnit = WeightUnit.Kilograms
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewName.Should().Be("Cable Row");
        result.Value.ProgressionTypeChanged.Should().BeTrue();
        result.Value.NewProgressionType.Should().Be("RepsPerSet");
    }

    [Fact]
    public async Task Handle_WhenInvalidProgressionConfig_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateActiveWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exercise = workout.Exercises.First();
        var command = new SubstituteExerciseCommand(
            workout.Id.Value,
            exercise.Id.Value,
            "Cable Row",
            "cable-row",
            NewProgressionConfig: new ProgressionConfigDto
            {
                Type = "InvalidType"
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    [Fact]
    public async Task Handle_WhenSubstitutionPushesChestOverTheCap_SucceedsAndCarriesOneWarning()
    {
        SetupAuthenticatedUser(TestUserId);
        _library.GetAllTemplatesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExerciseTemplate("Bench Press", EquipmentType.Barbell, MuscleGroup: "chest"),
            new ExerciseTemplate("Cable Fly", EquipmentType.Cable, MuscleGroup: "chest")
        });
        var workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .WithExercise(e => e.WithName("Dumbbell Fly").WithCategory(ExerciseCategory.Accessory).WithOrder(2).AsRepsPerSet(startingSets: 6, targetSets: 6))
            .Build();
        workout.Start();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(workout);
        var fly = workout.Exercises.Single(e => e.Name == "Dumbbell Fly");

        var result = await _handler.Handle(
            new SubstituteExerciseCommand(workout.Id.Value, fly.Id.Value, "Cable Fly", "cable-fly"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("volume warnings never block");
        result.Value.Warnings.Should().ContainSingle();
        result.Value.Warnings[0].Bucket.Should().Be("Chest");
        result.Value.Warnings[0].Sets.Should().Be(11, "5 bench sets + 6 cable fly sets");
        result.Value.Warnings[0].Cap.Should().Be(10);
        result.Value.OverTimeBudget.Should().BeFalse("1100 s + 780 s is 42 minutes with the warm-up");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
