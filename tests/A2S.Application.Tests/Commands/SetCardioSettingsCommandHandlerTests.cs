using System.Reflection;
using A2S.Application.Commands.CardioSettings;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class SetCardioSettingsCommandHandlerTests
{
    private static readonly UserId TestUserId = new("csc11111-1111-1111-1111-111111111111");

    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SetCardioSettingsCommandHandler _handler;
    private readonly Workout _workout;

    public SetCardioSettingsCommandHandlerTests()
    {
        _handler = new SetCardioSettingsCommandHandler(_workoutRepository, _unitOfWork, _currentUserService);
        _currentUserService.UserId.Returns(TestUserId.Value);
        _workout = new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithVariant(ProgramVariant.FourDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .WithExercise(b => b.WithDay(DayNumber.Day1).WithOrder(2).AsCardioStage(CardioLane.Easy))
            .WithDefaultLinearExercise("Squat", DayNumber.Day2, 1, 140m)
            .WithExercise(b => b.WithDay(DayNumber.Day2).WithOrder(2).AsCardioStage(CardioLane.Easy))
            .Build();
        _workout.Start();
        _workoutRepository.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);
    }

    private Exercise Walk(DayNumber day) =>
        _workout.Exercises.Single(e => e.AssignedDay == day && e.Category == ExerciseCategory.Cardio);

    [Fact]
    public async Task SetCardioSettings_SetsSpeedAndIncline_AndBumpsWorkoutArea()
    {
        var walk = Walk(DayNumber.Day1);

        var result = await _handler.Handle(
            new SetCardioSettingsCommand(_workout.Id.Value, walk.Id.Value, 5m, 5.5m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Id.Should().Be(walk.Id.Value);
        var progression = result.Value.Progression.Should().BeOfType<CardioStageProgressionDto>().Subject;
        progression.Prescription.Should().Be("10 min @ 5 % incline, 5.5 km/h");
        progression.IsSettingsPending.Should().BeFalse();
        Walk(DayNumber.Day2).Progression.CalculateCardioBlock()!.Text.Should().Be("10 min @ 5 % incline, 5.5 km/h");
        _workout.AuditEntries.Should().ContainSingle(a => a.Type == AuditEntryType.CardioSettingsChanged);
        _workoutRepository.Received(1).Update(_workout);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        typeof(SetCardioSettingsCommand).GetCustomAttribute<ChangesSyncAreaAttribute>()!.Areas.Should().Be(SyncArea.Workout);
    }

    [Fact]
    public async Task SetCardioSettings_OnALift_IsNotFound()
    {
        var bench = _workout.Exercises.Single(e => e.Name == "Bench Press");

        var result = await _handler.Handle(
            new SetCardioSettingsCommand(_workout.Id.Value, bench.Id.Value, 5m, null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCardioSettings_SomeoneElsesWorkout_IsNotFound()
    {
        _currentUserService.UserId.Returns("csc22222-2222-2222-2222-222222222222");

        var result = await _handler.Handle(
            new SetCardioSettingsCommand(_workout.Id.Value, Walk(DayNumber.Day1).Id.Value, 5m, 5.5m), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task SetCardioSettings_PausedWorkout_IsABusinessRuleViolation()
    {
        _workout.Pause();

        var result = await _handler.Handle(
            new SetCardioSettingsCommand(_workout.Id.Value, Walk(DayNumber.Day1).Id.Value, 5m, 5.5m), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(5.0, null, true)]
    [InlineData(null, 5.5, true)]
    [InlineData(0.0, null, false)]
    [InlineData(100.0, null, true)]
    [InlineData(100.5, null, false)]
    [InlineData(null, 0.4, false)]
    [InlineData(null, 0.5, true)]
    [InlineData(null, 25.0, true)]
    [InlineData(null, 25.1, false)]
    public void Validator_Bounds(double? intensity, double? speed, bool valid)
    {
        var command = new SetCardioSettingsCommand(_workout.Id.Value, Walk(DayNumber.Day1).Id.Value,
            (decimal?)intensity, (decimal?)speed);

        new SetCardioSettingsCommandValidator().Validate(command).IsValid.Should().Be(valid);
    }
}
