using A2S.Application.Commands.Mobility;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Mobility;

public class MobilityRoutineCommandHandlerTests
{
    private static readonly UserId Owner = new("user_mobility_cmd_0001");
    private static readonly UserId Stranger = new("user_mobility_cmd_0002");

    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly MobilityRoutine _routine;

    public MobilityRoutineCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Owner.Value);
        _routine = MobilityRoutine.Create(Owner, "Mobility A", 10,
            [new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, null, true)]);
        _routines.GetByIdAsync(_routine.Id, Arg.Any<CancellationToken>()).Returns(_routine);
        _plans.ListForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns(new List<TrainingPlan>());
    }

    private UpdateMobilityRoutineCommandHandler UpdateHandler() => new(_routines, _unitOfWork, _currentUser);

    private DeleteMobilityRoutineCommandHandler DeleteHandler() => new(_routines, _plans, _unitOfWork, _currentUser);

    private TrainingPlan PlanScheduling(MobilityRoutineId routine) =>
        TrainingPlan.Create(Owner, "Cut to 85", new DateOnly(2026, 11, 16),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"), ScheduledSession.Mobility(DayOfWeek.Wednesday, routine, "Mobility A")], 95m);

    [Fact]
    public async Task Update_RenamesAndSetsMinutes_AndSaves()
    {
        var result = await UpdateHandler().Handle(new UpdateMobilityRoutineCommand(_routine.Id.Value, "Gym warm-up", 12), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Name.Should().Be("Gym warm-up");
        result.Value.TargetMinutes.Should().Be(12);
        result.Value.Drills.Should().ContainSingle();
        _routines.Received(1).Update(_routine);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AnotherUsersRoutine_IsNotFound()
    {
        _currentUser.UserId.Returns(Stranger.Value);

        var result = await UpdateHandler().Handle(new UpdateMobilityRoutineCommand(_routine.Id.Value, "Mine now", 12), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        _routine.Name.Should().Be("Mobility A");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void UpdateValidator_RejectsBlankNameAndNonPositiveMinutes()
    {
        var validator = new UpdateMobilityRoutineCommandValidator();

        validator.Validate(new UpdateMobilityRoutineCommand(_routine.Id.Value, "", 12)).IsValid.Should().BeFalse();
        validator.Validate(new UpdateMobilityRoutineCommand(_routine.Id.Value, "Gym warm-up", 0)).IsValid.Should().BeFalse();
        validator.Validate(new UpdateMobilityRoutineCommand(_routine.Id.Value, "Gym warm-up", 12)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_UnscheduledRoutine_RemovesAndSaves()
    {
        var result = await DeleteHandler().Handle(new DeleteMobilityRoutineCommand(_routine.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        _routines.Received(1).Remove(_routine);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_RoutineInAPlanSchedule_IsAConflictAndKeepsIt()
    {
        _plans.ListForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns(new List<TrainingPlan> { PlanScheduling(_routine.Id) });

        var result = await DeleteHandler().Handle(new DeleteMobilityRoutineCommand(_routine.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Conflict);
        result.Error.Should().Contain("Cut to 85");
        _routines.DidNotReceive().Remove(Arg.Any<MobilityRoutine>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_AnotherUsersRoutine_IsNotFound()
    {
        _currentUser.UserId.Returns(Stranger.Value);

        var result = await DeleteHandler().Handle(new DeleteMobilityRoutineCommand(_routine.Id.Value), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        _routines.DidNotReceive().Remove(Arg.Any<MobilityRoutine>());
    }
}
