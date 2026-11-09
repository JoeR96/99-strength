using A2S.Application.Common;
using A2S.Application.Queries.Plans;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.Plans;

public class GetPlanCalendarQueryHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_calendar_0001");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000002"));
    private static readonly DateOnly Mon2Nov = new(2026, 11, 2);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IHevyLinkRepository _hevyLinks = Substitute.For<IHevyLinkRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetPlanCalendarQueryHandler _handler;

    public GetPlanCalendarQueryHandlerTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)));
        _hevyLinks.ListAsync(Arg.Any<UserId>(), Arg.Any<HevyLinkKind>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<HevyLink>());
        _handler = new GetPlanCalendarQueryHandler(_plans, _workouts, _hevyLinks, _currentUser, new PlanCalendarService(clock));
    }

    private static TrainingPlan PlanFor(UserId owner) =>
        TrainingPlan.Create(owner, "Plan", Mon2Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);

    [Fact]
    public async Task Handle_ResolvesPushedRoutines_PerDateForLifts_AndPerSlotForCardio()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(TestUserId);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(plan);
        var cardio = plan.Schedule.Single(s => s.Kind == SessionKind.Cardio);
        var now = DateTimeOffset.UtcNow;
        _hevyLinks.ListAsync(TestUserId, HevyLinkKind.Routine, Arg.Any<CancellationToken>()).Returns(
        [
            HevyLink.Create(TestUserId, HevyLinkKind.Routine, HevyLocalKeys.LiftRoutine(plan.Id, new DateOnly(2026, 11, 9), DayNumber.Day1), "rt_upper_a_w2", now),
            HevyLink.Create(TestUserId, HevyLinkKind.Routine, HevyLocalKeys.SlotRoutine(plan.Id, SessionKind.Cardio, cardio.Label), "rt_4x4", now)
        ]);

        var result = await _handler.Handle(new GetPlanCalendarQuery(2026, 11), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var days = result.Value.ToDictionary(d => d.Date);
        days[new DateOnly(2026, 11, 9)].Sessions.Single().HevyRoutineId.Should().Be("rt_upper_a_w2");
        days[new DateOnly(2026, 11, 16)].Sessions.Single().HevyRoutineId.Should().BeNull("only the 9th was pushed");
        // A cardio slot's routine is reused every week.
        days[new DateOnly(2026, 11, 4)].Sessions.Single().HevyRoutineId.Should().Be("rt_4x4");
        days[new DateOnly(2026, 11, 25)].Sessions.Single().HevyRoutineId.Should().Be("rt_4x4");
        days[new DateOnly(2026, 11, 8)].Sessions.Single().HevyRoutineId.Should().BeNull("rest days have no routine");
        await _hevyLinks.Received(1).ListAsync(TestUserId, HevyLinkKind.Routine, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAnInvalidMonth_FailsValidation()
    {
        var result = await _handler.Handle(new GetPlanCalendarQuery(2026, 13), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
    }
}
