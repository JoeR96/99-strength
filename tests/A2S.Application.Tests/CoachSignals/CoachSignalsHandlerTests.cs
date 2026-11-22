using A2S.Application.Cardio;
using A2S.Application.Commands.CardioTracks;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Push;
using A2S.Application.Queries.CardioTracks;
using A2S.Application.Readiness;
using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.CoachSignals;

public class CoachSignalsHandlerTests
{
    private static readonly UserId Owner = new("user_coach_app_0001");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 22);

    private readonly IReadinessRepository _readiness = Substitute.For<IReadinessRepository>();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IBodyWeightRepository _weights = Substitute.For<IBodyWeightRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));

    public CoachSignalsHandlerTests()
    {
        _currentUser.UserId.Returns(Owner.Value);
    }

    [Fact]
    public async Task SetReadiness_Red_CreatesTheEntry_AndSkipsTheFinisher()
    {
        var handler = new SetReadinessCommandHandler(_readiness, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new SetReadinessCommand(Today, ReadinessColour.Red), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Colour.Should().Be(ReadinessColour.Red);
        result.Value.FinisherSkipped.Should().BeTrue();
        result.Value.SkipHardCardio.Should().BeTrue();
        await _readiness.Received(1).AddAsync(Arg.Is<ReadinessEntry>(e => e.Date == Today && e.UserId == Owner), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetReadiness_Again_UpdatesTheSameDay()
    {
        var entry = ReadinessEntry.Create(Owner, Today, ReadinessColour.Green, Now);
        _readiness.GetForDateAsync(Owner, Today, Arg.Any<CancellationToken>()).Returns(entry);

        var result = await new SetReadinessCommandHandler(_readiness, _unitOfWork, _currentUser, _clock)
            .Handle(new SetReadinessCommand(Today, ReadinessColour.Yellow), CancellationToken.None);

        result.Value.Colour.Should().Be(ReadinessColour.Yellow);
        await _readiness.DidNotReceive().AddAsync(Arg.Any<ReadinessEntry>(), Arg.Any<CancellationToken>());
        _readiness.Received(1).Update(entry);
    }

    [Fact]
    public async Task KeepFinisher_UndoesTheSkip_AndFailsWithoutAnEntry()
    {
        var handler = new SetReadinessFinisherCommandHandler(_readiness, _unitOfWork, _currentUser, _clock);

        var missing = await handler.Handle(new SetReadinessFinisherCommand(Today, true), CancellationToken.None);
        missing.ErrorCode.Should().Be(ErrorCode.NotFound);

        _readiness.GetForDateAsync(Owner, Today, Arg.Any<CancellationToken>()).Returns(ReadinessEntry.Create(Owner, Today, ReadinessColour.Red, Now));
        var kept = await handler.Handle(new SetReadinessFinisherCommand(Today, true), CancellationToken.None);

        kept.Value.FinisherSkipped.Should().BeFalse();
        kept.Value.KeepFinisher.Should().BeTrue();
    }

    [Fact]
    public async Task GetReadiness_NothingTapped_ReturnsNullColour()
    {
        var result = await new GetReadinessQueryHandler(_readiness, _currentUser, _clock).Handle(new GetReadinessQuery(), CancellationToken.None);

        result.Value.Date.Should().Be(Today);
        result.Value.Colour.Should().BeNull();
        result.Value.FinisherSkipped.Should().BeFalse();
    }

    [Fact]
    public async Task ListTracks_GatedTrack_CarriesGateProgress_FromTrendAndPain()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Today, withGate: true);
        _tracks.ListForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns([run]);
        _weights.ListAsync(Owner, Arg.Any<DateOnly>(), Today, Arg.Any<CancellationToken>())
            .Returns([BodyWeightEntry.Create(Owner, Today, 89.5m, Now)]);
        var handler = new ListCardioTracksQueryHandler(_tracks, _currentUser, _clock, new CardioGateEvaluator(_weights, _plans, new TrendService()));

        var result = await handler.Handle(new ListCardioTracksQuery(), CancellationToken.None);

        var gate = result.Value.Single().Gate!;
        gate.Conditions.Should().HaveCount(3);
        gate.Conditions[0].Met.Should().BeTrue("trend 89.5 ≤ 90");
        gate.Conditions[1].Progress.Should().Be("Start that track first");
        gate.Conditions[2].Met.Should().BeTrue();
        gate.AllMet.Should().BeFalse();
        result.Value.Single().Status.Should().Be(CardioTrackStatus.Gated);
    }

    [Fact]
    public async Task SetGate_UseDefault_AppliesTheRunningGate_ThenStartActivates()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Today);
        _tracks.GetByIdAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);

        var gated = await new SetCardioTrackGateCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new SetCardioTrackGateCommand(run.Id.Value, null, UseDefault: true), CancellationToken.None);
        gated.Value.Status.Should().Be(CardioTrackStatus.Gated);
        gated.Value.StartGate.Should().HaveCount(3);

        var started = await new StartGatedCardioTrackCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new StartGatedCardioTrackCommand(run.Id.Value), CancellationToken.None);
        started.Value.Status.Should().Be(CardioTrackStatus.Active);
    }

    [Fact]
    public async Task SetGate_InvalidCondition_IsADomainRuleViolation()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Today);
        _tracks.GetByIdAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);

        var result = await new SetCardioTrackGateCommandHandler(_tracks, _unitOfWork, _currentUser, _clock)
            .Handle(new SetCardioTrackGateCommand(run.Id.Value, [new GateConditionDto(GateConditionType.NoPainInAreas, Weeks: 4)]), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    private TrainingPlan PlanWithBuildPhase(DateOnly buildStarts, WorkoutId? workout = null)
    {
        var plan = TrainingPlan.Create(Owner, "Plan", new DateOnly(2026, 11, 16), [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Lower A")], 95m);
        plan.AddPhase(new PlanPhase(PhaseType.Cut, buildStarts, buildStarts.AddDays(40), null, "Build",
            ["Stair finisher: start intervals"], [new PhaseSwap("Pause Squat", "Squat (Barbell)")]));
        if (workout is { } id)
        {
            plan.LinkWorkout(id);
        }

        _plans.GetActiveForUserAsync(Owner, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    [Fact]
    public async Task PhaseHeadsUp_InTheWeekBefore_ResolvesTheSwapToTheWorkoutExercise()
    {
        var workout = new WorkoutBuilder().WithUserId(Owner.Value).WithExercise(e => e.WithName("Pause Squat")).Build();
        var pause = workout.Exercises.Single(e => e.Name == "Pause Squat");
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);
        PlanWithBuildPhase(Today.AddDays(3), workout.Id);

        var result = await new GetPhaseHeadsUpQueryHandler(_plans, _workouts, _currentUser, _clock)
            .Handle(new GetPhaseHeadsUpQuery(), CancellationToken.None);

        var headsUp = result.Value!;
        headsUp.Label.Should().Be("Build");
        headsUp.DaysUntil.Should().Be(3);
        headsUp.Changes.Should().Equal("Stair finisher: start intervals");
        headsUp.Swaps.Single().ExerciseId.Should().Be(pause.Id.Value);
        headsUp.Swaps.Single().WorkoutId.Should().Be(workout.Id.Value);
        headsUp.Swaps.Single().Done.Should().BeFalse();
    }

    [Fact]
    public async Task PhaseHeadsUp_OutsideTheWindow_IsNull()
    {
        PlanWithBuildPhase(Today.AddDays(8));

        var result = await new GetPhaseHeadsUpQueryHandler(_plans, _workouts, _currentUser, _clock)
            .Handle(new GetPhaseHeadsUpQuery(), CancellationToken.None);

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task SetPhaseChanges_UpdatesThePhase()
    {
        var plan = PlanWithBuildPhase(Today.AddDays(3));
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await new SetPhaseChangesCommandHandler(_plans, _unitOfWork, _currentUser)
            .Handle(new SetPhaseChangesCommand(plan.Id.Value, Today.AddDays(3), "Build", ["+15 s holds"], []), CancellationToken.None);

        result.Value.Phases.Single().Changes.Should().Equal("+15 s holds");
        result.Value.Phases.Single().Swaps.Should().BeEmpty();
    }

    [Fact]
    public void PhaseChangeNudge_OnlyInsideTheWindowAndDaytime()
    {
        var phase = new PlanPhase(PhaseType.Cut, new DateOnly(2026, 11, 23), new DateOnly(2026, 12, 31), null, "Build",
            ["Back squat replaces pause squat"]);

        var nudge = PushNudgePlanner.PhaseChangeNudge(new TimeOnly(9, 0), Today, phase)!;
        nudge.Kind.Should().Be(PushNudgeKind.PhaseChange);
        nudge.Title.Should().Be("Build starts Monday 23 Nov");
        nudge.Body.Should().Be("Back squat replaces pause squat");

        PushNudgePlanner.PhaseChangeNudge(new TimeOnly(7, 59), Today, phase).Should().BeNull();
        PushNudgePlanner.PhaseChangeNudge(new TimeOnly(21, 0), Today, phase).Should().BeNull();
        PushNudgePlanner.PhaseChangeNudge(new TimeOnly(9, 0), Today.AddDays(-8), phase).Should().BeNull();
    }
}
