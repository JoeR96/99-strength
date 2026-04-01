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

    private static readonly UserId TestUserId = new(Guid.Parse("a1a1a1a1-b2b2-c3c3-d4d4-e5e5e5e5e5e5"));

    public ConfirmWorkingWeightCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new ConfirmWorkingWeightCommandHandler(_workoutRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenValid_ConfirmsWeightAndSaves()
    {
        var workout = CreateWorkout(TestUserId);
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
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new ConfirmWorkingWeightCommand(
            workout.Id.Value, Guid.NewGuid(), 55m, WeightUnit.Kilograms);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    private static Workout CreateWorkout(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithRepsPerSetProgression(
                "Cable Row", ExerciseCategory.Accessory, EquipmentType.Cable,
                DayNumber.Day1, 1, "CABLE001",
                RepRange.Create(8, 12), 3, 4, false,
                Weight.Create(50m, WeightUnit.Kilograms))
        };
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }
}
