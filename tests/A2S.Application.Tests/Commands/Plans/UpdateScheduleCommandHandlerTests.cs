using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Plans;

/// <summary>Review focus 3: moving the 4×4 next to a lower day warns, it does not block.</summary>
public class UpdateScheduleCommandHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_schedule_0001");

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly UpdateScheduleCommandHandler _handler;
    private readonly CardioProtocol _fourByFour = CardioProtocol.NorwegianFourByFour(TestUserId);
    private readonly TrainingPlan _plan;

    public UpdateScheduleCommandHandlerTests()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plan = TrainingPlan.Create(TestUserId, "Cut to 85 — Nov 2026", new DateOnly(2026, 11, 11),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);
        _protocols.ListForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(new List<CardioProtocol> { _fourByFour });
        _handler = new UpdateScheduleCommandHandler(_plans, _protocols, _unitOfWork, _currentUser);
    }

    private static ScheduledSessionDto Lift(DayOfWeek day, DayNumber liftDay, string label) =>
        new(day, SessionKind.Lift, liftDay, null, null, label);

    private ScheduledSessionDto FourByFour(DayOfWeek day) =>
        new(day, SessionKind.Cardio, null, _fourByFour.Id.Value, null, "Cardio");

    private static IReadOnlyList<ScheduledSessionDto> Week(ScheduledSessionDto cardio) =>
    [
        Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
        Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
        cardio,
        Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B"),
        Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B"),
        new(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")
    ];

    [Fact]
    public async Task Handle_DefaultWeek_SavesWithNoWarnings()
    {
        var result = await _handler.Handle(new UpdateScheduleCommand(_plan.Id.Value, Week(FourByFour(DayOfWeek.Wednesday))), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Warnings.Should().BeEmpty();
        result.Value.Plan.Schedule.Should().HaveCount(6);
        _plan.Schedule.Should().HaveCount(6);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FourByFourOnMonday_SavesAndWarnsAboutLowerA()
    {
        var result = await _handler.Handle(new UpdateScheduleCommand(_plan.Id.Value, Week(FourByFour(DayOfWeek.Monday))), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Warnings.Should().ContainSingle();
        result.Value.Warnings[0].Code.Should().Be(PlanScheduleRules.FourByFourBeforeLowerDay);
        result.Value.Warnings[0].Message.Should().Contain("Lower A");
        _plan.Schedule.Should().Contain(s => s.Day == DayOfWeek.Monday && s.Kind == SessionKind.Cardio, "a warning never blocks the change");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SomeoneElsesPlan_IsNotFound()
    {
        _currentUser.UserId.Returns("user_plans_schedule_0002");

        var result = await _handler.Handle(new UpdateScheduleCommand(_plan.Id.Value, Week(FourByFour(DayOfWeek.Wednesday))), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_RestOnlyWeek_IsADomainRuleViolation()
    {
        var result = await _handler.Handle(
            new UpdateScheduleCommand(_plan.Id.Value, [new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
