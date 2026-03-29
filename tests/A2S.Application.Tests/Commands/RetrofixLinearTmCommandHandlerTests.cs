using A2S.Application.Commands.RetrofixLinearTm;
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

public class RetrofixLinearTmCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly RetrofixLinearTmCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.Parse("d1d1d1d1-e2e2-f3f3-a4a4-b5b5b5b5b5b5");
    private static readonly Guid TestWorkoutId = Guid.Parse("c2c2c2c2-d3d3-e4e4-f5f5-a6a6a6a6a6a6");
    private static readonly Guid TestExerciseId = Guid.Parse("b3b3b3b3-c4c4-d5d5-e6e6-f7f7f7f7f7f7");
    private static readonly Guid NonExistentExerciseId = Guid.Parse("a4a4a4a4-b5b5-c6c6-d7d7-e8e8e8e8e8e8");

    public RetrofixLinearTmCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(new UserId(TestUserId));
        _handler = new RetrofixLinearTmCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenExerciseNotFound_ReturnsFailure()
    {
        var workout = CreateLinearWorkout();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new RetrofixLinearTmCommand(TestWorkoutId, NonExistentExerciseId, 100m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenExerciseNotLinear_ReturnsFailure()
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithRepsPerSetProgression(
                "Cable Row", ExerciseCategory.Accessory, EquipmentType.Cable,
                DayNumber.Day1, 1, "CABLE001",
                RepRange.Create(8, 10, 12), 3, 4, false,
                Weight.Create(50m, WeightUnit.Kilograms))
        };
        var workout = Workout.Create(new UserId(TestUserId), "Test", ProgramVariant.FiveDay, exercises);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var exerciseId = workout.Exercises.First().Id.Value;
        var command = new RetrofixLinearTmCommand(TestWorkoutId, exerciseId, 100m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not use Linear progression");
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsFailure()
    {
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);

        var command = new RetrofixLinearTmCommand(TestWorkoutId, TestExerciseId, 100m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Workout not found");
    }

    private static Workout CreateLinearWorkout()
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "SQ001",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 4)
        };
        return Workout.Create(new UserId(TestUserId), "Test Linear", ProgramVariant.FiveDay, exercises);
    }
}
