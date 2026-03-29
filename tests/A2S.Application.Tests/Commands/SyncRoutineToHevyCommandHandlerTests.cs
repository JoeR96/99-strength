using A2S.Application.Commands.SyncRoutineToHevy;
using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class SyncRoutineToHevyCommandHandlerTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IHevyIntegrationService _hevyService;
    private readonly IA2SProgramProvider _programProvider;
    private readonly ILogger<SyncRoutineToHevyCommandHandler> _logger;
    private readonly SyncRoutineToHevyCommandHandler _handler;

    private static readonly Guid TestWorkoutId = Guid.Parse("e1e1e1e1-f2f2-a3a3-b4b4-c5c5c5c5c5c5");
    private static readonly Guid TestUserId = Guid.Parse("f2f2f2f2-a3a3-b4b4-c5c5-d6d6d6d6d6d6");

    public SyncRoutineToHevyCommandHandlerTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _hevyService = Substitute.For<IHevyIntegrationService>();
        _programProvider = Substitute.For<IA2SProgramProvider>();
        _logger = Substitute.For<ILogger<SyncRoutineToHevyCommandHandler>>();
        _handler = new SyncRoutineToHevyCommandHandler(
            _workoutRepository, _hevyService, _programProvider, _logger);
    }

    [Fact]
    public async Task Handle_WhenHevyServiceThrows_ReturnsFailure()
    {
        var workout = CreateWorkout();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);
        _programProvider.GetWeekParameters(1)
            .Returns(new WeekParameters { WeekNumber = 1, BlockNumber = 1, Intensity = 0.70m, Sets = 4, TargetReps = 5, IsDeload = false });
        _hevyService.SyncRoutineForDayAsync(Arg.Any<HevySyncRoutineRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var command = new SyncRoutineToHevyCommand(TestWorkoutId, 1, 1, "test-api-key");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to sync routine");
    }

    [Fact]
    public async Task Handle_WhenInvalidWeekNumber_ReturnsFailure()
    {
        var workout = CreateWorkout();
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);

        var command = new SyncRoutineToHevyCommand(TestWorkoutId, 99, 1, "test-api-key");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Week number must be between");
    }

    private static Workout CreateWorkout()
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "SQ001",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 4)
        };
        return Workout.Create(new UserId(TestUserId), "Test", ProgramVariant.FiveDay, exercises);
    }
}
