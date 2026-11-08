using A2S.Application.Common;
using A2S.Application.Queries.Plans;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.Plans;

public class GetTodayQueryHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_today_0001");
    private static readonly UserId OtherUserId = new("user_plans_today_0002");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-000000000001"));
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateOnly Wed18Nov = new(2026, 11, 18);
    private static readonly DateOnly Thu19Nov = new(2026, 11, 19);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetTodayQueryHandler _handler;

    public GetTodayQueryHandlerTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)));
        _handler = new GetTodayQueryHandler(_plans, _workouts, _currentUser, new PlanCalendarService(clock), clock);
    }

    private static TrainingPlan PlanFor(UserId owner) =>
        TrainingPlan.Create(owner, "Plan", Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
            ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B"),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);

    [Fact]
    public async Task Handle_OnAPausedDay_ReturnsPausedSessions()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(TestUserId);
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Wed18Nov, new DateOnly(2026, 11, 22), "Away"));
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await _handler.Handle(new GetTodayQuery(Wed18Nov), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Date.Should().Be(Wed18Nov);
        result.Value.Phase.Should().Be(PhaseType.Pause);
        result.Value.Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Paused, SessionStatus.Paused);
        result.Value.Sessions.Select(s => s.Label).Should().Equal("Norwegian 4×4", "Mobility B");
        result.Value.Sessions[0].CardioProtocolId.Should().Be(FourByFour.Value);
        result.Value.Sessions[1].MobilityRoutineId.Should().Be(MobilityB.Value);
        result.Value.Week.WeekStart.Should().Be(Mon16Nov);
        result.Value.Week.LiftsPlanned.Should().Be(1);
        result.Value.Week.CardioPlanned.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithoutADate_UsesTheLocalToday()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(TestUserId);
        plan.CompleteSession(Thu19Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day3,
            recordedAt: new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero));
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await _handler.Handle(new GetTodayQuery(null), CancellationToken.None);

        result.Value.Date.Should().Be(Thu19Nov);
        result.Value.Sessions.Should().ContainSingle();
        result.Value.Sessions[0].Status.Should().Be(SessionStatus.Done);
        result.Value.Sessions[0].CompletionId.Should().Be(plan.Completions.Single().Id.Value);
        result.Value.Sessions[0].LiftDay.Should().Be(DayNumber.Day3);
        result.Value.Week.LiftsDone.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithALinkedWorkout_LoadsItsHistory()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(TestUserId);
        var workoutId = new WorkoutId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
        plan.LinkWorkout(workoutId);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(plan);
        _workouts.GetByIdAsync(workoutId, Arg.Any<CancellationToken>()).Returns((Workout?)null);

        var result = await _handler.Handle(new GetTodayQuery(Thu19Nov), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sessions[0].Status.Should().Be(SessionStatus.Planned);
        await _workouts.Received(1).GetByIdAsync(workoutId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheActivePlanBelongsToSomeoneElse_ReturnsNotFound()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(PlanFor(OtherUserId));

        var result = await _handler.Handle(new GetTodayQuery(Wed18Nov), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_WithNoActivePlan_ReturnsNotFound()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);

        var result = await _handler.Handle(new GetTodayQuery(null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetTodayQuery(null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
