using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Plans;

public class CreateTrainingPlanCommandHandlerTests
{
    private static readonly UserId TestUserId = new("user_plans_create_0001");
    private static readonly UserId OtherUserId = new("user_plans_create_0002");

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly CreateTrainingPlanCommandHandler _handler;

    public CreateTrainingPlanCommandHandlerTests()
    {
        _handler = new CreateTrainingPlanCommandHandler(_plans, _workouts, _unitOfWork, _currentUser);
    }

    private static CreateTrainingPlanCommand Command(Guid? linkedWorkoutId = null) => new(
        "Cut to 85 — Nov 2026",
        new DateOnly(2026, 11, 11),
        [
            new ScheduledSessionDto(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A"),
            new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")
        ],
        95m,
        FlexibleMeals: 1,
        AlcoholOccasions: 1,
        LinkedWorkoutId: linkedWorkoutId);

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
        await _plans.DidNotReceive().AddAsync(Arg.Any<TrainingPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SnapsTheStartToTheComingMondayAndPersists()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StartDate.Should().Be(new DateOnly(2026, 11, 16));
        result.Value.IsActive.Should().BeTrue();
        result.Value.Schedule.Should().HaveCount(2);
        result.Value.Allowances.Should().Be(new WeeklyAllowancesDto(1, 1));
        await _plans.Received(1).AddAsync(Arg.Is<TrainingPlan>(p => p.UserId == TestUserId && p.StartDate == new DateOnly(2026, 11, 16)), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeactivatesTheCurrentlyActivePlan()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var existing = TrainingPlan.Create(TestUserId, "Old", new DateOnly(2026, 10, 5),
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "A")], 96m);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.IsActive.Should().BeFalse();
        _plans.Received(1).Update(existing);
    }

    [Fact]
    public async Task Handle_WithLinkedWorkout_LinksIt()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithDefaultLinearExercise().Build();
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(Command(workout.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LinkedWorkoutId.Should().Be(workout.Id.Value);
    }

    [Fact]
    public async Task Handle_WithSomeoneElsesWorkout_ReturnsNotFound()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var workout = new WorkoutBuilder().WithUserId(OtherUserId).WithDefaultLinearExercise().Build();
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(Command(workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        await _plans.DidNotReceive().AddAsync(Arg.Any<TrainingPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheDomainRejectsThePlan_ReturnsDomainRuleViolation()
    {
        _currentUser.UserId.Returns(TestUserId.Value);
        var restOnly = Command() with
        {
            Schedule = [new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")]
        };

        var result = await _handler.Handle(restOnly, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }
}
