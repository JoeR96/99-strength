using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class CreateWorkoutCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExerciseLibraryProvider _exerciseLibrary;
    private readonly CreateWorkoutCommandHandler _handler;

    private static readonly UserId TestUserId = new("aaa44444-4444-4444-4444-444444444444");

    public CreateWorkoutCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _exerciseLibrary = Substitute.For<IExerciseLibraryProvider>();
        _handler = new CreateWorkoutCommandHandler(
            _workoutRepository, _unitOfWork, _currentUserService, _exerciseLibrary);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        _currentUserService.UserId.Returns((string?)null);

        var command = new CreateWorkoutCommand("Test Workout", ProgramVariant.FiveDay);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("authenticated");
    }

    [Fact]
    public async Task Handle_WhenActiveWorkoutExists_ReturnsFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        var existingWorkout = CreateWorkout(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(existingWorkout);

        var command = new CreateWorkoutCommand("Test Workout", ProgramVariant.FiveDay);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active workout already exists");
    }

    [Fact]
    public async Task Handle_WhenValidWithDefaultExercises_CreatesAndReturnsId()
    {
        SetupAuthenticatedUser(TestUserId);
        SetupDefaultExerciseLibrary();
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var command = new CreateWorkoutCommand("Test Workout", ProgramVariant.FiveDay);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _workoutRepository.Received(1).AddAsync(Arg.Any<Workout>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithConfiguredExercises_UsesExerciseLibrary()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetActiveWorkoutAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);
        _exerciseLibrary.GetByName("Squat")
            .Returns(new ExerciseTemplate("Squat", EquipmentType.Barbell));

        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                ExternalTemplateId = "SQ001",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 120m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand("Test Workout", ProgramVariant.FiveDay, Exercises: exercises);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _exerciseLibrary.Received(1).GetByName("Squat");
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private void SetupDefaultExerciseLibrary()
    {
        _exerciseLibrary.GetByName("Squat (Barbell)")
            .Returns(new ExerciseTemplate("Squat (Barbell)", EquipmentType.Barbell));
        _exerciseLibrary.GetByName("Bench Press (Barbell)")
            .Returns(new ExerciseTemplate("Bench Press (Barbell)", EquipmentType.Barbell));
        _exerciseLibrary.GetByName("Deadlift (Barbell)")
            .Returns(new ExerciseTemplate("Deadlift (Barbell)", EquipmentType.Barbell));
        _exerciseLibrary.GetByName("Overhead Press (Barbell)")
            .Returns(new ExerciseTemplate("Overhead Press (Barbell)", EquipmentType.Barbell));
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
