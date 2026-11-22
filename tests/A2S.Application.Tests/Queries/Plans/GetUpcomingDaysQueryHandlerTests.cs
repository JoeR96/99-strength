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

public class GetUpcomingDaysQueryHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_upcoming_0001");
    private static readonly DateOnly Mon2Nov = new(2026, 11, 2);
    private static readonly DateOnly Sun22Nov = new(2026, 11, 22);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IHevyLinkRepository _hevyLinks = Substitute.For<IHevyLinkRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetUpcomingDaysQueryHandler _handler;

    public GetUpcomingDaysQueryHandlerTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero)));
        _hevyLinks.ListAsync(Arg.Any<UserId>(), Arg.Any<HevyLinkKind>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<HevyLink>());
        _currentUser.UserId.Returns(TestUserId.Value);
        _handler = new GetUpcomingDaysQueryHandler(_plans, _workouts, _hevyLinks, _currentUser, new PlanCalendarService(clock), clock);
    }

    private void GivenPlan() =>
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(
            TrainingPlan.Create(TestUserId, "Plan", Mon2Nov,
            [
                ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day1, "Upper A"),
                ScheduledSession.Rest(DayOfWeek.Sunday)
            ], 95m));

    [Fact]
    public async Task Handle_WithoutFrom_ReturnsTheNextDaysFromTodayInOrder()
    {
        GivenPlan();

        var result = await _handler.Handle(new GetUpcomingDaysQuery(null, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(d => d.Date).Should().Equal(Sun22Nov, Sun22Nov.AddDays(1), Sun22Nov.AddDays(2));
        result.Value[0].Sessions.Should().ContainSingle().Which.Kind.Should().Be(SessionKind.Rest);
        result.Value[1].Sessions.Should().BeEmpty();
        result.Value[2].Sessions.Should().ContainSingle().Which.Label.Should().Be("Upper A");
        result.Value[2].Sessions[0].Status.Should().Be(SessionStatus.Planned);
    }

    [Fact]
    public async Task Handle_FromAGivenDate_StartsThere()
    {
        GivenPlan();

        var result = await _handler.Handle(new GetUpcomingDaysQuery(new DateOnly(2026, 12, 1), 14), CancellationToken.None);

        result.Value.Should().HaveCount(14);
        result.Value[0].Date.Should().Be(new DateOnly(2026, 12, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public async Task Handle_WithDaysOutOfRange_FailsValidation(int days)
    {
        var result = await _handler.Handle(new GetUpcomingDaysQuery(null, days), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Handle_WithNoActivePlan_Fails()
    {
        var result = await _handler.Handle(new GetUpcomingDaysQuery(null, 14), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
