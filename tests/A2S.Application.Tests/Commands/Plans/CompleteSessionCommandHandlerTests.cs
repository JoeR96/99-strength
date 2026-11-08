using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Plans;

public class CompleteSessionCommandHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_complete_0001");
    private static readonly UserId OtherUserId = new("user_plans_complete_0002");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly CompleteSessionCommandHandler _handler;

    public CompleteSessionCommandHandlerTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(Now));
        _handler = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, clock, _protocols);
    }

    private static TrainingPlan PlanFor(UserId owner) =>
        TrainingPlan.Create(owner, "Plan", Mon16Nov,
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"), ScheduledSession.Rest(DayOfWeek.Sunday)], 95m);

    private TrainingPlan GivenOwnedPlan()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(TestUserId);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    [Fact]
    public async Task Handle_TwiceWithTheSameHevyId_RecordsOneCompletion()
    {
        var plan = GivenOwnedPlan();
        var command = new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Lift, LiftDay: DayNumber.Day1, HevyWorkoutId: "hevy-77");

        var first = await _handler.Handle(command, CancellationToken.None);
        var second = await _handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);
        first.Value.Source.Should().Be(CompletionSource.Hevy);
        first.Value.HevyWorkoutId.Should().Be("hevy-77");
        plan.Completions.Should().HaveCount(1);
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StampsTheCompletionWithTheClock()
    {
        var plan = GivenOwnedPlan();

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Lift, LiftDay: DayNumber.Day1), CancellationToken.None);

        result.Value.RecordedAtUtc.Should().Be(Now);
        result.Value.Source.Should().Be(CompletionSource.Manual);
        result.Value.Date.Should().Be(Mon16Nov);
    }

    [Fact]
    public async Task Handle_CardioWithLog_KeepsTheLog()
    {
        var plan = GivenOwnedPlan();
        var protocol = Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001");

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, new DateOnly(2026, 11, 18), SessionKind.Cardio,
                CardioProtocolId: protocol, Cardio: new CardioLogDto(2580, 160, 8, 5200m, "Hills")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CardioProtocolId.Should().Be(protocol);
        result.Value.Cardio.Should().Be(new CardioLogDto(2580, 160, 8, 5200m, "Hills"));
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(
            new CompleteSessionCommand(Guid.Parse("11111111-1111-1111-1111-111111111111"), Mon16Nov, SessionKind.Lift, LiftDay: DayNumber.Day1),
            CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Fact]
    public async Task Handle_ForSomeoneElsesPlan_ReturnsNotFound()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var plan = PlanFor(OtherUserId);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Lift, LiftDay: DayNumber.Day1), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        plan.Completions.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownPlan_ReturnsNotFound()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetByIdAsync(Arg.Any<TrainingPlanId>(), Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);

        var result = await _handler.Handle(
            new CompleteSessionCommand(Guid.Parse("22222222-2222-2222-2222-222222222222"), Mon16Nov, SessionKind.Lift, LiftDay: DayNumber.Day1),
            CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_RestKind_ReturnsDomainRuleViolation()
    {
        var plan = GivenOwnedPlan();

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, new DateOnly(2026, 11, 22), SessionKind.Rest), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Handle_CardioMarkedDoneWithoutALog_CreditsTheProtocolsZoneMinutes()
    {
        var plan = GivenOwnedPlan();
        var fourByFour = CardioProtocol.NorwegianFourByFour(TestUserId);
        _protocols.GetByIdAsync(fourByFour.Id, Arg.Any<CancellationToken>()).Returns(fourByFour);

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Cardio, CardioProtocolId: fourByFour.Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cardio.Should().NotBeNull();
        result.Value.Cardio!.HighIntensityMinutes.Should().Be(16);
        result.Value.Cardio.DurationSeconds.Should().Be(40 * 60);
    }

    [Fact]
    public async Task Handle_CardioLogWithWatchZoneMinutes_KeepsTheWatchValue()
    {
        var plan = GivenOwnedPlan();
        var fourByFour = CardioProtocol.NorwegianFourByFour(TestUserId);
        _protocols.GetByIdAsync(fourByFour.Id, Arg.Any<CancellationToken>()).Returns(fourByFour);

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Cardio, CardioProtocolId: fourByFour.Id.Value,
                Cardio: new CardioLogDto(2500, 168, 9, null, "windy", HighIntensityMinutes: 21)),
            CancellationToken.None);

        result.Value.Cardio!.HighIntensityMinutes.Should().Be(21);
        await _protocols.DidNotReceive().GetByIdAsync(Arg.Any<CardioProtocolId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LissLogWithoutZoneMinutes_StaysNull_ForTheWatchToFill()
    {
        var plan = GivenOwnedPlan();
        var liss = CardioProtocol.Liss(TestUserId, 35, null);
        _protocols.GetByIdAsync(liss.Id, Arg.Any<CancellationToken>()).Returns(liss);

        var result = await _handler.Handle(
            new CompleteSessionCommand(plan.Id.Value, Mon16Nov, SessionKind.Cardio, CardioProtocolId: liss.Id.Value,
                Cardio: new CardioLogDto(2100, 130, 4, 3500m, null)),
            CancellationToken.None);

        result.Value.Cardio!.HighIntensityMinutes.Should().BeNull();
    }
}
