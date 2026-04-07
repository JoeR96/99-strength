using A2S.Application.Commands.ConfirmWorkingWeight;
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

public class ConfirmWorkingWeightCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ConfirmWorkingWeightCommandHandler _handler;

    private static readonly UserId TestUserId = new("a1a1a1a1-b2b2-c3c3-d4d4-e5e5e5e5e5e5");

    public ConfirmWorkingWeightCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new ConfirmWorkingWeightCommandHandler(_workoutRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenValid_ConfirmsWeightAndSaves()
    {
        var workout = CreateWorkoutWithPendingWeight(TestUserId);
        var exerciseId = workout.Exercises.First().Id.Value;
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new ConfirmWorkingWeightCommand(
            workout.Id.Value, exerciseId, 55m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExerciseNotFound_ReturnsFailure()
    {
        var workout = CreateWorkoutWithPendingWeight(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new ConfirmWorkingWeightCommand(
            workout.Id.Value, Guid.NewGuid(), 55m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    /// <summary>
    /// Creates a workout with a Cable exercise in pending weight confirmation state.
    /// Cable exercises at max sets trigger pending weight confirmation upon progression.
    /// </summary>
    private static Workout CreateWorkoutWithPendingWeight(UserId userId)
    {
        var weight = Weight.Create(50m, WeightUnit.Kilograms);
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithRepsPerSetProgression(
                "Cable Row", ExerciseCategory.Accessory, EquipmentType.Cable,
                DayNumber.Day1, 1, "CABLE001",
                RepRange.Create(8, 12), 3, 3, false,
                weight)
        };
        var workout = Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
        workout.Start();

        var exercise = workout.Exercises.First();
        var plannedSets = new List<PlannedSet>
        {
            new(1, weight, 12),
            new(2, weight, 12),
            new(3, weight, 12)
        };
        var completedSets = new List<CompletedSet>
        {
            new(1, weight, 12),
            new(2, weight, 12),
            new(3, weight, 12)
        };
        var performance = new ExercisePerformance(exercise.Id, plannedSets, completedSets);
        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        return workout;
    }
}
