using A2S.Application.Common;
using A2S.Application.Queries.SuggestedPlans;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.SuggestedPlans;

public class GetSuggestedPlansQueryHandlerTests
{
    private static readonly UserId TestUserId = new("user_seed_preview_0001");
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetSuggestedPlansQueryHandler _handler;

    public GetSuggestedPlansQueryHandlerTests()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);
        _handler = new GetSuggestedPlansQueryHandler(_plans, _currentUser, new LocalClock(new FakeTimeProvider(Now)));
    }

    [Fact]
    public async Task Handle_PreviewsTheCutTo85PlanForTheComingMonday()
    {
        var result = await _handler.Handle(new GetSuggestedPlansQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var preview = result.Value.Should().ContainSingle().Subject;
        preview.Key.Should().Be("cut-to-85");
        preview.Name.Should().Be("Cut to 85 — Nov 2026");
        preview.StartDate.Should().Be(new DateOnly(2026, 11, 16));
        preview.AlreadyStarted.Should().BeFalse();
        preview.Inputs.Select(i => i.Key).Should().Equal("benchTm", "squatTm", "ohpTm", "deadliftTm", "startWeightKg");
        preview.Schedule.Should().HaveCount(8);
        preview.Schedule[0].Should().Be(new Application.DTOs.SuggestedSessionPreviewDto(DayOfWeek.Monday, SessionKind.Lift, "Upper A"));
        preview.LiftingDays.Select(d => d.Label).Should().Equal("Upper A", "Lower A", "Upper B", "Lower B");
        preview.LiftingDays[0].Exercises.Should().HaveCount(5).And.StartWith("Bench Press (Barbell)");
        preview.LiftingDays.Select(d => (d.EstimatedMinutes, d.OverBudget)).Should().Equal((60, true), (62, true), (48, false), (62, true));
        preview.Habits.Should().HaveCount(5);
        preview.Checkpoints.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_WhenThePlanIsAlreadyActive_SaysSo()
    {
        var active = TrainingPlan.Create(TestUserId, "Cut to 85 — Nov 2026", new DateOnly(2026, 11, 9),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(active);

        var result = await _handler.Handle(new GetSuggestedPlansQuery(), CancellationToken.None);

        result.Value.Single().AlreadyStarted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetSuggestedPlansQuery(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
