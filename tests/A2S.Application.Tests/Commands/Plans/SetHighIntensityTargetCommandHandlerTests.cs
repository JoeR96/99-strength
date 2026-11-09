using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Plans;

public class SetHighIntensityTargetCommandHandlerTests
{
    private static readonly UserId Owner = new("user_zone_target_0001");
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private TrainingPlan GivenOwnedPlan()
    {
        _currentUser.UserId.Returns(Owner.Value);
        var plan = TrainingPlan.Create(Owner, "Cut", new DateOnly(2026, 11, 16),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    [Fact]
    public async Task Handle_SetsTheBand_AndPersists()
    {
        var plan = GivenOwnedPlan();
        var handler = new SetHighIntensityTargetCommandHandler(_plans, _unitOfWork, _currentUser);

        var result = await handler.Handle(new SetHighIntensityTargetCommand(plan.Id.Value, 20, 40), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new A2S.Application.DTOs.HighIntensityTargetDto(20, 40));
        plan.HighIntensityTarget.Should().Be(new HighIntensityTarget(20, 40));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MinAboveMax_IsADomainRuleViolation()
    {
        var plan = GivenOwnedPlan();
        var handler = new SetHighIntensityTargetCommandHandler(_plans, _unitOfWork, _currentUser);

        var result = await handler.Handle(new SetHighIntensityTargetCommand(plan.Id.Value, 40, 20), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsMaxBelowMin()
    {
        var validator = new SetHighIntensityTargetCommandValidator();

        validator.Validate(new SetHighIntensityTargetCommand(Guid.NewGuid(), 30, 15)).IsValid.Should().BeFalse();
        validator.Validate(new SetHighIntensityTargetCommand(Guid.NewGuid(), 15, 30)).IsValid.Should().BeTrue();
    }
}
