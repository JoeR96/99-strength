using A2S.Application.Cardio;
using A2S.Application.Commands.CardioTracks;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.CardioTracks;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.CardioTracks;

public class CardioTrackCommandTests
{
    private static readonly UserId Owner = new("user_tracks_app_0001");
    private static readonly UserId Other = new("user_tracks_app_0002");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));
    private readonly CardioTrackSessionRecorder _recorder;

    public CardioTrackCommandTests()
    {
        _currentUser.UserId.Returns(Owner.Value);
        _recorder = new CardioTrackSessionRecorder(_tracks, _users, _habits);
        _habits.ListForUserAsync(Owner, false, Arg.Any<CancellationToken>()).Returns(Array.Empty<HabitDefinition>());
    }

    private CardioTrack GivenTrack(string template, UserId? owner = null)
    {
        var track = CardioTrackTemplates.Create(template, owner ?? Owner, Mon16Nov);
        _tracks.GetByIdAsync(track.Id, Arg.Any<CancellationToken>()).Returns(track);
        return track;
    }

    private static CardioLogDto EasyLog(int minutes = 20) => new(minutes * 60, null, null, null, null, Effort: EffortRating.Easy);

    [Fact]
    public async Task Create_FromTemplate_AddsAndSaves()
    {
        var handler = new CreateCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new CreateCardioTrackCommand(CardioTrackTemplates.StairFinisher, "Stairs"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Stairs");
        result.Value.StartedOn.Should().Be(Mon16Nov);
        result.Value.Prescription.Title.Should().Be("5 min continuous @ level 6");
        result.Value.SessionsToStepUp.Should().Be(2);
        result.Value.SafetyNote.Should().Contain("chest pain");
        await _tracks.Received(1).AddAsync(Arg.Any<CardioTrack>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UnknownTemplate_IsADomainFailure()
    {
        var handler = new CreateCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new CreateCardioTrackCommand("nope"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Log_EvaluatesTrack_AndClientKeyMakesRetriesIdempotent()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var handler = new LogCardioTrackSessionCommandHandler(_recorder, _unitOfWork, _currentUser, _clock);
        var command = new LogCardioTrackSessionCommand(track.Id.Value, EasyLog(), ClientKey: "offline-1");

        var first = await handler.Handle(command, CancellationToken.None);
        var retry = await handler.Handle(command, CancellationToken.None);

        first.Value.Outcome.Should().Be(TrackOutcome.Qualified);
        first.Value.Track.SessionsToStepUp.Should().Be(1);
        retry.Value.AlreadyRecorded.Should().BeTrue();
        track.Sessions.Should().ContainSingle().Which.Date.Should().Be(Mon16Nov);
    }

    [Fact]
    public async Task Log_UsesTheUsersZonesForTheHeartRateCeiling()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var user = User.Create("z@example.com", "Z", Owner, new DateTime(2026, 1, 1));
        user.SetHeartRate(200, null);
        _users.GetByIdAsync(Owner, Arg.Any<CancellationToken>()).Returns(user);
        var handler = new LogCardioTrackSessionCommandHandler(_recorder, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new LogCardioTrackSessionCommand(track.Id.Value, EasyLog() with { AvgHeartRate = 150 }), CancellationToken.None);

        result.Value.Outcome.Should().Be(TrackOutcome.Held);
        result.Value.Reason.Should().Contain("zone 2");
    }

    [Fact]
    public async Task Log_Walk_TicksTheWalksHabitOnce()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var walks = HabitDefinition.Create(Owner, "Walks", HabitType.Counter, 5, 3, null, 1);
        _habits.ListForUserAsync(Owner, false, Arg.Any<CancellationToken>()).Returns(new[] { walks });
        var handler = new LogCardioTrackSessionCommandHandler(_recorder, _unitOfWork, _currentUser, _clock);
        var command = new LogCardioTrackSessionCommand(track.Id.Value, EasyLog(), ClientKey: "walk-1");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        await _habits.Received(1).AddLogAsync(Arg.Is<HabitLog>(l => l.HabitId == walks.Id && l.Value == 1 && l.Date == Mon16Nov),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Log_Stairs_DoesNotTouchHabits()
    {
        var track = GivenTrack(CardioTrackTemplates.StairFinisher);
        var handler = new LogCardioTrackSessionCommandHandler(_recorder, _unitOfWork, _currentUser, _clock);

        await handler.Handle(new LogCardioTrackSessionCommand(track.Id.Value, EasyLog(5)), CancellationToken.None);

        await _habits.DidNotReceiveWithAnyArgs().ListForUserAsync(default, default, default);
    }

    [Fact]
    public async Task AnotherUsersTrack_IsNotFound()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder, Other);

        var log = await new LogCardioTrackSessionCommandHandler(_recorder, _unitOfWork, _currentUser, _clock)
            .Handle(new LogCardioTrackSessionCommand(track.Id.Value, EasyLog()), CancellationToken.None);
        var undo = await new UndoCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new UndoCardioTrackCommand(track.Id.Value), CancellationToken.None);
        var detail = await new GetCardioTrackQueryHandler(_tracks, _currentUser, _clock)
            .Handle(new GetCardioTrackQuery(track.Id.Value), CancellationToken.None);

        log.ErrorCode.Should().Be(ErrorCode.NotFound);
        undo.ErrorCode.Should().Be(ErrorCode.NotFound);
        detail.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Undo_WithNothingToUndo_IsADomainFailure()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);

        var result = await new UndoCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new UndoCardioTrackCommand(track.Id.Value), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Decide_AcceptsTheSuggestion()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkToRun);
        track.LogSession("a", Mon16Nov, new CardioLog(1800, null, null, null, null), false, null, Now);
        track.LogSession("b", Mon16Nov, new CardioLog(1800, null, null, null, null), true, null, Now);
        track.LogSession("c", Mon16Nov, new CardioLog(1800, null, null, null, null), true, null, Now);

        var result = await new DecideCardioTrackSuggestionCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new DecideCardioTrackSuggestionCommand(track.Id.Value, Accept: true), CancellationToken.None);

        result.Value.Suggestion.Should().Be(TrackSuggestion.None);
        result.Value.State.Week.Should().Be(1);
    }

    [Fact]
    public async Task Pause_ThenResume()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var handler = new SetCardioTrackPausedCommandHandler(_tracks, _unitOfWork, _currentUser, _clock);

        (await handler.Handle(new SetCardioTrackPausedCommand(track.Id.Value, true), CancellationToken.None)).Value.Status
            .Should().Be(CardioTrackStatus.Paused);
        (await handler.Handle(new SetCardioTrackPausedCommand(track.Id.Value, false), CancellationToken.None)).Value.Status
            .Should().Be(CardioTrackStatus.Active);
    }

    [Fact]
    public async Task Update_EditsStagesAndName()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var stages = track.Stages.Select(s => s.ToDto()).ToList();
        stages[0] = stages[0] with { DoubleProgression = stages[0].DoubleProgression! with { VolumeMin = 25 } };

        var result = await new UpdateCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new UpdateCardioTrackCommand(track.Id.Value, "Evening walk", "Flat route", stages, null), CancellationToken.None);

        result.Value.Name.Should().Be("Evening walk");
        result.Value.Notes.Should().Be("Flat route");
        result.Value.Prescription.Title.Should().Be("25 min continuous");
        result.Value.Benchmark.Should().BeNull();
    }

    [Fact]
    public async Task Benchmark_RecordsPersonalBest()
    {
        var track = GivenTrack(CardioTrackTemplates.StairFinisher);

        var result = await new RecordCardioBenchmarkCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new RecordCardioBenchmarkCommand(track.Id.Value, 31), CancellationToken.None);

        result.Value.IsPersonalBest.Should().BeTrue();
        result.Value.Date.Should().Be(Mon16Nov);
    }

    [Fact]
    public async Task HeartRateZones_SetAndRead()
    {
        var user = User.Create("z@example.com", "Z", Owner, new DateTime(2026, 1, 1));
        _users.GetByIdAsync(Owner, Arg.Any<CancellationToken>()).Returns(user);

        var set = await new SetHeartRateZonesCommandHandler(_users, _unitOfWork, _currentUser)
            .Handle(new SetHeartRateZonesCommand(190, null), CancellationToken.None);
        var read = await new GetHeartRateZonesQueryHandler(_users, _currentUser)
            .Handle(new GetHeartRateZonesQuery(), CancellationToken.None);

        set.Value.MaxHeartRate.Should().Be(190);
        read.Value.Bands[1].UpperBpm.Should().Be(133);
        read.Value.LowerPercents.Should().Equal(50, 60, 70, 80, 90);
    }

    [Fact]
    public async Task CompleteSession_ForATrackSlot_EvaluatesTheTrackOncePerCompletion()
    {
        var track = GivenTrack(CardioTrackTemplates.StairFinisher);
        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov,
            [ScheduledSession.Cardio(DayOfWeek.Monday, null, "Stair finisher", track.Id)], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, _clock, _protocols, _recorder);
        var command = new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Cardio,
            Cardio: new CardioLogDto(300, 160, null, null, null, 4, Effort: EffortRating.Brutal, Floors: 30));

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.Value.CardioTrackId.Should().Be(track.Id.Value);
        second.Value.Id.Should().Be(first.Value.Id);
        track.Sessions.Should().ContainSingle().Which.Key.Should().Be($"completion:{first.Value.Id:N}");
        track.Sessions[0].Floors.Should().Be(30);
        track.State.Qualifying.Should().Be(1);
    }

    [Fact]
    public async Task CompleteSession_MarkDoneWithoutLog_UsesThePrescription()
    {
        var track = GivenTrack(CardioTrackTemplates.WalkBuilder);
        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov, [ScheduledSession.Cardio(DayOfWeek.Friday, null, "Cardio")], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, _clock, _protocols, _recorder);

        await handler.Handle(new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Cardio, CardioTrackId: track.Id.Value),
            CancellationToken.None);

        track.Sessions.Should().ContainSingle().Which.DurationSeconds.Should().Be(1200);
        track.State.Qualifying.Should().Be(1);
    }

    [Fact]
    public async Task TodayCardio_ListsActivePrescriptions_AndWarnsAboveTheWeeklyMax()
    {
        var stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Owner, Mon16Nov);
        stairs.LogSession("manual:1", Mon16Nov, new CardioLog(600, null, null, null, null, 10), true, null, Now);
        var paused = CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Owner, Mon16Nov);
        paused.Pause();
        _tracks.ListForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns(new[] { stairs, paused });
        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov, [ScheduledSession.Cardio(DayOfWeek.Friday, null, "Cardio")], 95m);
        plan.CompleteSession(Mon16Nov, SessionKind.Cardio, CompletionSource.Manual, log: new CardioLog(2400, null, null, null, null, 25),
            recordedAt: Now);
        _plans.GetActiveForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await new GetTodayCardioQueryHandler(_tracks, _plans, _currentUser, _clock)
            .Handle(new GetTodayCardioQuery(), CancellationToken.None);

        result.Value.Tracks.Should().ContainSingle().Which.Name.Should().Be("Stair finisher");
        result.Value.HighIntensityMinutesThisWeek.Should().Be(35);
        result.Value.Warnings.Should().ContainSingle().Which.Code.Should().Be("HighIntensityAboveMax");
    }

    [Fact]
    public async Task CompleteSession_WeekProgramSlots_AMissedPlannedSessionBlocksTheWeek()
    {
        var run = GivenTrack(CardioTrackTemplates.WalkToRun);
        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov,
        [
            ScheduledSession.Cardio(DayOfWeek.Tuesday, null, "Run", run.Id),
            ScheduledSession.Cardio(DayOfWeek.Thursday, null, "Run", run.Id)
        ], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, _clock, _protocols, _recorder);
        var easy = new CardioLogDto(1800, null, null, null, null, Effort: EffortRating.Easy);

        foreach (var day in new[] { 1, 8, 10 })
        {
            await handler.Handle(new CompleteSessionCommand(plan.Id.Value, Mon16Nov.AddDays(day), SessionKind.Cardio, Cardio: easy), CancellationToken.None);
        }

        run.Sessions.Should().HaveCount(3);
        run.State.Week.Should().Be(1);
        run.State.Suggestion.Should().Be(TrackSuggestion.RepeatWeek, "Thursday 19 Nov was planned and missed");
        run.Audit[^1].Reason.Should().Contain("missed");
    }

    [Fact]
    public async Task CompleteSession_WeekProgramSlots_AllPlannedDone_AdvancesAfterThree()
    {
        var run = GivenTrack(CardioTrackTemplates.WalkToRun);
        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov,
        [
            ScheduledSession.Cardio(DayOfWeek.Tuesday, null, "Run", run.Id),
            ScheduledSession.Cardio(DayOfWeek.Thursday, null, "Run", run.Id)
        ], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, _clock, _protocols, _recorder);
        var easy = new CardioLogDto(1800, null, null, null, null, Effort: EffortRating.Easy);

        foreach (var day in new[] { 1, 3 })
        {
            await handler.Handle(new CompleteSessionCommand(plan.Id.Value, Mon16Nov.AddDays(day), SessionKind.Cardio, Cardio: easy), CancellationToken.None);
        }

        run.State.Week.Should().Be(1, "walk → run needs 3 qualifying sessions even at 2 a week");
        await handler.Handle(new CompleteSessionCommand(plan.Id.Value, Mon16Nov.AddDays(8), SessionKind.Cardio, Cardio: easy), CancellationToken.None);
        run.State.Week.Should().Be(2);
    }
}
