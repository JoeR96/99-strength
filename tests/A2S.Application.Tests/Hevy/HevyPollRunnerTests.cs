using A2S.Application.Commands.Hevy;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

/// <summary>The 15-minute poll tick: only users with a key and a session today are pulled, each as themselves.</summary>
public class HevyPollRunnerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Ann = new("user_ann");
    private static readonly UserId Bob = new("user_bob");
    private static readonly DateTimeOffset MondayEvening = new(2026, 11, 16, 18, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly AmbientUser _ambient = new();
    private readonly List<string?> _pulledAs = new();
    private readonly HevyPollRunner _runner;

    public HevyPollRunnerTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(MondayEvening));
        _runner = new HevyPollRunner(_users, _plans, _sender, _ambient, clock, NullLogger<HevyPollRunner>.Instance);
        _sender.Send(Arg.Any<PullHevyEventsCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _pulledAs.Add(_ambient.UserId);
                return Result.Success(new PullSummary(1, 1, 0, 0));
            });
    }

    private static TrainingPlan PlanWith(UserId owner, params ScheduledSession[] sessions) =>
        TrainingPlan.Create(owner, "Cut", new DateOnly(2026, 11, 9), sessions, 95m);

    [Fact]
    public async Task PullsOnlyUsersWithAKey_AndASessionToday_AsThatUser()
    {
        _users.ListIdsWithHevyKeyAsync(Arg.Any<CancellationToken>()).Returns(new List<UserId> { Joe, Ann, Bob });
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>())
            .Returns(PlanWith(Joe, ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")));
        _plans.GetActiveForUserAsync(Ann, Arg.Any<CancellationToken>())
            .Returns(PlanWith(Ann, ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day1, "Upper A"), ScheduledSession.Rest(DayOfWeek.Monday)));
        _plans.GetActiveForUserAsync(Bob, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);

        var pulled = await _runner.RunOnceAsync();

        pulled.Should().Be(1);
        _pulledAs.Should().Equal(Joe.Value);
        _ambient.UserId.Should().BeNull("the scope is clean after the tick");
        await _sender.Received(1).Send(Arg.Any<PullHevyEventsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NobodyHasAKey_NothingIsPulled()
    {
        _users.ListIdsWithHevyKeyAsync(Arg.Any<CancellationToken>()).Returns(new List<UserId>());

        var pulled = await _runner.RunOnceAsync();

        pulled.Should().Be(0);
        await _sender.DidNotReceive().Send(Arg.Any<PullHevyEventsCommand>(), Arg.Any<CancellationToken>());
        await _plans.DidNotReceive().GetActiveForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task APausedPlan_IsSkipped()
    {
        _users.ListIdsWithHevyKeyAsync(Arg.Any<CancellationToken>()).Returns(new List<UserId> { Joe });
        var plan = PlanWith(Joe, ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"));
        plan.AddPhase(new PlanPhase(PhaseType.Pause, new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 20), "Away"));
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(plan);

        (await _runner.RunOnceAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AFailedPull_IsCountedAndDoesNotStopTheOthers()
    {
        _users.ListIdsWithHevyKeyAsync(Arg.Any<CancellationToken>()).Returns(new List<UserId> { Joe, Ann });
        foreach (var user in new[] { Joe, Ann })
        {
            _plans.GetActiveForUserAsync(user, Arg.Any<CancellationToken>())
                .Returns(PlanWith(user, ScheduledSession.Cardio(DayOfWeek.Monday, null, "Run")));
        }
        _sender.Send(Arg.Any<PullHevyEventsCommand>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Result.Failure<PullSummary>("Hevy is unavailable, your change is saved locally", ErrorCode.ExternalServiceError),
                _ => Result.Success(new PullSummary(0, 0, 0, 0)));

        (await _runner.RunOnceAsync()).Should().Be(2);
    }
}
