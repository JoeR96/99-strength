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

public class RenamePlanCommandHandlerTests
{
    private static readonly UserId Owner = new("user_rename_plan_0001");
    private static readonly UserId Stranger = new("user_rename_plan_0002");
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private TrainingPlan GivenPlan(UserId signedIn)
    {
        _currentUser.UserId.Returns(signedIn.Value);
        var plan = TrainingPlan.Create(Owner, "Cut to 85 — Nov 2026", new DateOnly(2026, 11, 16),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    private RenamePlanCommandHandler Handler() => new(_plans, _unitOfWork, _currentUser);

    [Fact]
    public async Task Handle_RenamesTheOwnersPlan_AndPersists()
    {
        var plan = GivenPlan(Owner);

        var result = await Handler().Handle(new RenamePlanCommand(plan.Id.Value, " Lumbridge Lard Shed "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Name.Should().Be("Lumbridge Lard Shed");
        plan.Name.Should().Be("Lumbridge Lard Shed");
        _plans.Received(1).Update(plan);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AnotherUsersPlan_IsNotFound_AndUnchanged()
    {
        var plan = GivenPlan(Stranger);

        var result = await Handler().Handle(new RenamePlanCommand(plan.Id.Value, "Mine now"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        plan.Name.Should().Be("Cut to 85 — Nov 2026");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BlankName_IsADomainRuleViolation()
    {
        var plan = GivenPlan(Owner);

        var result = await Handler().Handle(new RenamePlanCommand(plan.Id.Value, "   "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Lumbridge Lard Shed", true)]
    public void Validator_NeedsANonBlankName(string name, bool valid)
    {
        new RenamePlanCommandValidator().Validate(new RenamePlanCommand(Guid.Parse("11111111-0000-0000-0000-000000000001"), name))
            .IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_RefusesMoreThan200Characters()
    {
        var validator = new RenamePlanCommandValidator();
        var id = Guid.Parse("11111111-0000-0000-0000-000000000001");

        validator.Validate(new RenamePlanCommand(id, new string('a', 201))).IsValid.Should().BeFalse();
        validator.Validate(new RenamePlanCommand(id, new string('a', 200))).IsValid.Should().BeTrue();
    }
}
