using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Common;
using A2S.Application.SuggestedPlans;
using A2S.Application.Tests.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
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

namespace A2S.Application.Tests.Commands.SuggestedPlans;

public class StartSuggestedPlanCommandHandlerTests
{
    private static readonly UserId TestUserId = new("user_seed_start_0001");
    // Wednesday 11 Nov 2026, 18:30 London → the plan starts Monday 16 Nov.
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Mon16Nov2026 = new(2026, 11, 16);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));
    private readonly StartSuggestedPlanCommandHandler _handler;

    private readonly List<TrainingPlan> _addedPlans = [];
    private readonly List<Workout> _addedWorkouts = [];
    private readonly List<CardioProtocol> _addedProtocols = [];
    private readonly List<MobilityRoutine> _addedRoutines = [];
    private readonly List<HabitDefinition> _addedHabits = [];

    public StartSuggestedPlanCommandHandlerTests()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);
        _workouts.GetActiveWorkoutAsync(TestUserId, Arg.Any<CancellationToken>()).Returns((Workout?)null);
        _habits.ListForUserAsync(TestUserId, true, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition>());

        _plans.AddAsync(Arg.Do<TrainingPlan>(_addedPlans.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _workouts.AddAsync(Arg.Do<Workout>(_addedWorkouts.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _protocols.AddAsync(Arg.Do<CardioProtocol>(_addedProtocols.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _routines.AddAsync(Arg.Do<MobilityRoutine>(_addedRoutines.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _habits.AddAsync(Arg.Do<HabitDefinition>(_addedHabits.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _handler = new StartSuggestedPlanCommandHandler(
            _plans, _workouts, _protocols, _routines, _habits, new SeedLibrary(), _unitOfWork, _currentUser, _clock);
    }

    private static StartSuggestedPlanCommand Command(decimal startWeight = 95m) => new(100m, 140m, 60m, 180m, startWeight);

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
        _addedPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CreatesOnePlanOneWorkoutThreeProtocolsTwoRoutinesFiveHabits_InOneSave()
    {
        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.AlreadyStarted.Should().BeFalse();
        result.Value.StartDate.Should().Be(Mon16Nov2026);
        result.Value.Warnings.Should().BeEmpty();

        _addedPlans.Should().ContainSingle();
        _addedWorkouts.Should().ContainSingle();
        _addedProtocols.Should().HaveCount(3);
        _addedRoutines.Should().HaveCount(2);
        _addedHabits.Should().HaveCount(5);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        var plan = _addedPlans[0];
        result.Value.PlanId.Should().Be(plan.Id.Value);
        result.Value.WorkoutId.Should().Be(_addedWorkouts[0].Id.Value);
        plan.LinkedWorkoutId.Should().Be(_addedWorkouts[0].Id);
    }

    [Fact]
    public async Task Handle_BuildsTheWorkoutFromTheSeed_StartedWithTheGivenTrainingMaxes()
    {
        await _handler.Handle(Command(), CancellationToken.None);

        var workout = _addedWorkouts.Single();
        workout.Name.Should().Be(CutTo85Nov2026.Name);
        workout.Variant.Should().Be(ProgramVariant.FourDay);
        workout.Status.Should().Be(WorkoutStatus.Active);
        workout.Exercises.Should().HaveCount(20);
        workout.Exercises.Single(e => e.Name == "Bench Press (Barbell)").GetTrainingMax()!.Value.Should().Be(100m);
        workout.Exercises.Single(e => e.Name == "Squat (Barbell)").GetTrainingMax()!.Value.Should().Be(140m);
        workout.Exercises.Single(e => e.Name == "Overhead Press (Barbell)").GetTrainingMax()!.Value.Should().Be(60m);
        workout.Exercises.Single(e => e.Name == "Deadlift (Barbell)").GetTrainingMax()!.Value.Should().Be(180m);
        workout.Exercises.Single(e => e.Name == "Single Arm Cable Row").IsUnilateral().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WiresTheScheduleToTheCreatedProtocolsAndRoutines()
    {
        await _handler.Handle(Command(), CancellationToken.None);

        var plan = _addedPlans.Single();
        var fourByFour = _addedProtocols.Single(p => p.Kind == CardioProtocolKind.NorwegianFourByFour);
        var mobilityB = _addedRoutines.Single(r => r.Name == "Mobility B");

        plan.Schedule.Should().HaveCount(8);
        plan.Schedule.Single(s => s.Day == DayOfWeek.Wednesday && s.Kind == SessionKind.Cardio).CardioProtocolId.Should().Be(fourByFour.Id);
        plan.Schedule.Single(s => s.Day == DayOfWeek.Wednesday && s.Kind == SessionKind.Mobility).MobilityRoutineId.Should().Be(mobilityB.Id);
        plan.Schedule.Single(s => s.Day == DayOfWeek.Saturday).CardioProtocolId.Should().BeNull("C25K or LISS is an open slot");
        plan.Schedule.Where(s => s.Kind == SessionKind.Lift).Select(s => s.LiftDay).Should().Equal(DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4);

        _addedProtocols.Select(p => p.Kind).Should().BeEquivalentTo([CardioProtocolKind.NorwegianFourByFour, CardioProtocolKind.CouchToFiveK, CardioProtocolKind.Liss]);
        _addedProtocols.Single(p => p.Kind == CardioProtocolKind.CouchToFiveK).WeekIndex.Should().Be(1);
        _addedProtocols.Single(p => p.Kind == CardioProtocolKind.Liss).TotalMinutes.Should().Be(35);
        _addedRoutines.Select(r => r.Name).Should().Equal("Mobility A", "Mobility B");
    }

    [Fact]
    public async Task Handle_AddsThePhasesCheckpointsStartWeightAndAllowances()
    {
        var result = await _handler.Handle(Command(startWeight: 96.5m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var plan = _addedPlans.Single();
        plan.Name.Should().Be("Cut to 85 — Nov 2026");
        plan.StartDate.Should().Be(Mon16Nov2026);
        plan.StartWeightKg.Should().Be(96.5m);
        plan.IsActive.Should().BeTrue();
        plan.Allowances.Should().Be(new WeeklyAllowances(1, 1));
        plan.Phases.Select(p => p.Type).Should().Equal(PhaseType.Cut, PhaseType.Maintenance, PhaseType.Cut);
        plan.Checkpoints.Select(c => (c.Date, c.TargetKg)).Should().Equal(
            (new DateOnly(2026, 12, 7), 92m),
            (new DateOnly(2026, 12, 21), 91m),
            (new DateOnly(2027, 2, 1), 88m),
            (new DateOnly(2027, 3, 8), 85m));
    }

    /// <summary>Review focus 1: started on a Wednesday, nothing is Missed before the Monday start.</summary>
    [Fact]
    public async Task Handle_StartedMidweek_HasNoSessionsBeforeMondayAndNothingMissed()
    {
        await _handler.Handle(Command(), CancellationToken.None);
        var plan = _addedPlans.Single();
        var calendar = new PlanCalendarService(_clock);

        plan.SessionsOn(new DateOnly(2026, 11, 12)).Should().BeEmpty("Thursday 12 Nov is before the start");
        var week = calendar.Week(plan, new DateOnly(2026, 11, 11), Array.Empty<Domain.ValueObjects.WorkoutActivity>());
        week.SelectMany(d => d.Sessions).Should().BeEmpty();

        var monday = calendar.DayFor(plan, Mon16Nov2026, Array.Empty<Domain.ValueObjects.WorkoutActivity>());
        monday.Sessions.Should().ContainSingle().Which.Status.Should().Be(SessionStatus.Planned);
        monday.Phase.Should().Be(PhaseType.Cut);
    }

    [Fact]
    public async Task Handle_CreatesTheHabitsWithTheirKeysAndTargets()
    {
        await _handler.Handle(Command(), CancellationToken.None);

        _addedHabits.Select(h => (h.Name, h.Type, h.Target, h.Minimum, h.ExternalKey)).Should().Equal(
            ("Water (532ml bottle)", HabitType.Counter, 6m, 4m, "seed:water"),
            ("Walks", HabitType.Counter, 2m, 1m, "terran:walks"),
            ("Morning weigh-in", HabitType.Numeric, 1m, 1m, "seed:weigh-in"),
            ("Mobility A done", HabitType.Checkbox, 1m, 1m, "seed:mobility-a"),
            ("Creatine (3–5g)", HabitType.Checkbox, 1m, 1m, "terran:creatine"));
    }

    [Fact]
    public async Task Handle_KeepsHabitsAlreadyImportedFromTerran_AndUnarchivesAMatchingName()
    {
        var walks = HabitDefinition.Create(TestUserId, "Daily walks", HabitType.Counter, 3, 1, null, 1, externalKey: "terran:walks");
        var creatine = HabitDefinition.Create(TestUserId, "Creatine (3–5g)", HabitType.Checkbox, 1, 1, null, 2);
        creatine.Archive();
        _habits.ListForUserAsync(TestUserId, true, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition> { walks, creatine });

        await _handler.Handle(Command(), CancellationToken.None);

        _addedHabits.Select(h => h.Name).Should().Equal("Water (532ml bottle)", "Morning weigh-in", "Mobility A done");
        walks.Target.Should().Be(3m, "the Terran target is kept, not overwritten");
        creatine.Archived.Should().BeFalse();
        _habits.Received(1).Update(creatine);
    }

    /// <summary>Review focus 2: a double tap or a second device creates one plan and one workout.</summary>
    [Fact]
    public async Task Handle_Twice_ReturnsTheExistingPlanWithoutCreatingAnother()
    {
        var first = await _handler.Handle(Command(), CancellationToken.None);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(_addedPlans.Single());

        var second = await _handler.Handle(Command(), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.AlreadyStarted.Should().BeTrue();
        second.Value.PlanId.Should().Be(first.Value.PlanId);
        second.Value.WorkoutId.Should().Be(first.Value.WorkoutId);
        second.Value.StartDate.Should().Be(first.Value.StartDate);
        _addedPlans.Should().ContainSingle();
        _addedWorkouts.Should().ContainSingle();
        _addedProtocols.Should().HaveCount(3);
        _addedHabits.Should().HaveCount(5);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAnotherActivePlan_DeactivatesIt()
    {
        var other = TrainingPlan.Create(TestUserId, "Old plan", new DateOnly(2026, 10, 5),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "A")], 97m);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(other);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.AlreadyStarted.Should().BeFalse();
        other.IsActive.Should().BeFalse();
        _plans.Received(1).Update(other);
        _addedPlans.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithAnActiveWorkout_PausesItLikeSetActiveWorkoutDoes()
    {
        var existing = new WorkoutBuilder().WithUserId(TestUserId).WithDefaultLinearExercise().Build();
        existing.Start();
        _workouts.GetActiveWorkoutAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        existing.Status.Should().Be(WorkoutStatus.Paused);
        _workouts.Received(1).Update(existing);
        _addedWorkouts.Single().Status.Should().Be(WorkoutStatus.Active);
    }

    [Fact]
    public async Task Handle_WhenALibraryTemplateIsMissing_FailsWithoutSaving()
    {
        var library = Substitute.For<Application.Services.IExerciseLibraryProvider>();
        library.GetByName(Arg.Any<string>()).Returns((Application.Services.ExerciseTemplate?)null);
        var handler = new StartSuggestedPlanCommandHandler(
            _plans, _workouts, _protocols, _routines, _habits, library, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Bench Press (Barbell)").And.Contain("exercise-library.json");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _addedPlans.Should().BeEmpty();
    }
}
