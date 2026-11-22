using A2S.Application.Commands.Mobility;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Mobility;

public class DrillProgressionCommandTests
{
    private static readonly UserId Owner = new("user_mobility_prog_0001");
    private static readonly UserId Stranger = new("user_mobility_prog_0002");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 19, 0, 0, TimeSpan.Zero);
    private const string Ladder = "Reverse plank ladder";

    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private readonly MobilityRoutine _routine;

    public DrillProgressionCommandTests()
    {
        _currentUser.UserId.Returns(Owner.Value);
        _clock.NowUtc().Returns(Now);
        _routine = MobilityRoutine.Create(Owner, "Home yoga", 20,
        [
            MobilityDrill.WithLevels(Ladder,
            [
                new DrillLevel("Level 1", "Reverse tabletop, knees bent", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), null),
                new DrillLevel("Level 2", "Hands on a bench, straight legs", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), null)
            ], 0, SideBias.None, false)
        ], MobilityRoutineKind.YogaFlow);
        _routines.GetByIdAsync(_routine.Id, Arg.Any<CancellationToken>()).Returns(_routine);
    }

    private Task<Result<DrillFeedbackResultDto>> Feedback(string key, DrillOutcome outcome) =>
        new RecordDrillFeedbackCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new RecordDrillFeedbackCommand(_routine.Id.Value, key, [new DrillFeedbackDto(Ladder, outcome)]), CancellationToken.None);

    [Fact]
    public async Task Feedback_TwoCleanSessions_ReturnsTheStepUpAndSaves()
    {
        await Feedback("s1", DrillOutcome.Clean);
        var result = await Feedback("s2", DrillOutcome.Clean);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Changes.Should().ContainSingle().Which.Kind.Should().Be(DrillChangeKind.DoseIncreased);
        result.Value.Changes[0].At.Should().Be(Now);
        result.Value.Routine.Drills[0].Dose.Amount.Should().Be(20);
        result.Value.Routine.CanUndo.Should().BeTrue();
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Feedback_OnAnotherUsersRoutine_IsNotFound()
    {
        _currentUser.UserId.Returns(Stranger.Value);

        var result = await Feedback("s1", DrillOutcome.Clean);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptSuggestion_MovesUpALevel()
    {
        _routine.ReplaceDrills([_routine.Drills[0].WithProgress(0, DrillDose.Seconds(30), 0, 1)]);

        var result = await new RespondToDrillSuggestionCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new RespondToDrillSuggestionCommand(_routine.Id.Value, Ladder, true), CancellationToken.None);

        result.Value.Drills[0].CurrentLevel.Should().Be(1);
        result.Value.Drills[0].Dose.Amount.Should().Be(15);
        result.Value.RecentChanges![0].Kind.Should().Be(DrillChangeKind.LevelUp);
    }

    [Fact]
    public async Task RespondToSuggestion_WithoutOne_IsABadRequestNotAnException()
    {
        var result = await new RespondToDrillSuggestionCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new RespondToDrillSuggestionCommand(_routine.Id.Value, Ladder, true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HoldIncrease_ThenUndo_RestoresTheHolds()
    {
        var bumped = await new ApplyHoldIncreaseCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new ApplyHoldIncreaseCommand(_routine.Id.Value, 15), CancellationToken.None);
        bumped.Value.Drills[0].Dose.Amount.Should().Be(30);

        var undone = await new UndoDrillProgressionCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new UndoDrillProgressionCommand(_routine.Id.Value), CancellationToken.None);

        undone.Value.Drills[0].Dose.Amount.Should().Be(15);
        undone.Value.CanUndo.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_WithNothingToUndo_IsAConflict()
    {
        var result = await new UndoDrillProgressionCommandHandler(_routines, _unitOfWork, _currentUser, _clock)
            .Handle(new UndoDrillProgressionCommand(_routine.Id.Value), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Conflict);
    }

    [Fact]
    public void Validators_RejectBadInput()
    {
        new ApplyHoldIncreaseCommandValidator().Validate(new ApplyHoldIncreaseCommand(_routine.Id.Value, 0)).IsValid.Should().BeFalse();
        new RecordDrillFeedbackCommandValidator().Validate(new RecordDrillFeedbackCommand(_routine.Id.Value, "", [])).IsValid.Should().BeFalse();
        new CreateMobilityRoutineCommandValidator().Validate(
            new CreateMobilityRoutineCommand("Video", 25, [], MobilityRoutineKind.FollowAlong, "https://example.com")).IsValid.Should().BeTrue();
        new CreateMobilityRoutineCommandValidator().Validate(new CreateMobilityRoutineCommand("Stretch", 10, [])).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRoutine_WithAKind_ChangesItAndKeepsDrills()
    {
        var result = await new UpdateMobilityRoutineCommandHandler(_routines, _unitOfWork, _currentUser)
            .Handle(new UpdateMobilityRoutineCommand(_routine.Id.Value, "Home stretch", 20, MobilityRoutineKind.Stretch), CancellationToken.None);

        result.Value.Kind.Should().Be(MobilityRoutineKind.Stretch);
        result.Value.Drills.Should().ContainSingle();
    }

    [Fact]
    public void DrillDto_RoundTripsLevelsAndProgress()
    {
        var dto = _routine.Drills[0].WithProgress(0, DrillDose.Seconds(25), 1, null).ToDto();

        dto.ToDomain().Should().Be(_routine.Drills[0].WithProgress(0, DrillDose.Seconds(25), 1, null));
        dto.Levels.Should().HaveCount(2);
        dto.Levels![0].Range.Should().Be(new DoseRangeDto(15, 30, 5));
    }
}
