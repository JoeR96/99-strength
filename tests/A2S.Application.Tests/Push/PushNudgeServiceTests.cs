using A2S.Application.Push;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Push;

/// <summary>
/// The nudge tick with a fake clock. Monday 9 Nov 2026 is in GMT, so London local time equals UTC there.
/// The clock starts in June (a FakeTimeProvider only moves forward) and each test sets its instant.
/// </summary>
public class PushNudgeServiceTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Today = new(2026, 11, 9); // Monday

    private static readonly DateTimeOffset MondaySessionStart = new(2026, 11, 9, 17, 30, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly IPushSubscriptionRepository _pushes = Substitute.For<IPushSubscriptionRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly LocalClock _clock;
    private readonly List<PushSubscription> _subscriptions = new();

    public PushNudgeServiceTests()
    {
        _clock = new LocalClock(_time);
        _pushes.ListSubscribedUsersAsync(Arg.Any<CancellationToken>()).Returns(_ => new List<UserId> { Joe });
        _pushes.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(_ => _subscriptions.ToList());
        _pushes.HasNudgeAsync(Joe, Arg.Any<DateOnly>(), Arg.Any<PushNudgeKind>(), Arg.Any<CancellationToken>()).Returns(false);
        _sender.SendAsync(Arg.Any<PushTarget>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>()).Returns(PushSendStatus.Sent);
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition>());
        _habits.GetLogsAsync(Joe, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new List<HabitLog>());
    }

    private PushNudgeService Service(bool configured = true) => new(
        _pushes,
        _plans,
        _workouts,
        _habits,
        new PlanCalendarService(_clock),
        _clock,
        _sender,
        _unitOfWork,
        Options.Create(configured
            ? new PushOptions { VapidPublicKey = "pub", VapidPrivateKey = "priv", Subject = "mailto:test@example.com" }
            : new PushOptions()),
        NullLogger<PushNudgeService>.Instance);

    private PushSubscription Subscribe(string endpoint, TimeOnly? sessionStart = null)
    {
        var sub = PushSubscription.Create(Joe, endpoint, "p256", "auth", null, _time.GetUtcNow(), sessionStart);
        _subscriptions.Add(sub);
        return sub;
    }

    private TrainingPlan PlanWithMondayLift()
    {
        var plan = TrainingPlan.Create(Joe, "Cut", new DateOnly(2026, 11, 2), new[] { ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A") }, 95m);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    [Fact]
    public async Task NoKeysConfigured_DoesNothing()
    {
        _time.SetUtcNow(MondaySessionStart);
        Subscribe("https://push.example.com/a");
        PlanWithMondayLift();

        var sent = await Service(configured: false).RunOnceAsync();

        sent.Should().Be(0);
        await _pushes.DidNotReceive().ListSubscribedUsersAsync(Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().SendAsync(Arg.Any<PushTarget>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AtSessionStart_SendsTimeToTrain_ToEveryDevice_AndLogsItOnce()
    {
        _time.SetUtcNow(MondaySessionStart);
        var phone = Subscribe("https://push.example.com/phone");
        var laptop = Subscribe("https://push.example.com/laptop");
        PlanWithMondayLift();

        var sent = await Service().RunOnceAsync();

        sent.Should().Be(1);
        await _sender.Received(1).SendAsync(
            Arg.Is<PushTarget>(t => t.Endpoint == phone.Endpoint),
            Arg.Is<PushMessage>(m => m.Title == "Time to train — Upper A" && m.Url == "/today"),
            Arg.Any<CancellationToken>());
        await _sender.Received(1).SendAsync(Arg.Is<PushTarget>(t => t.Endpoint == laptop.Endpoint), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
        await _pushes.Received(1).AddNudgeAsync(
            Arg.Is<PushNudgeLog>(l => l.UserId == Joe && l.LocalDate == Today && l.Kind == PushNudgeKind.SessionStart),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadySentToday_IsNotSentAgain()
    {
        _time.SetUtcNow(MondaySessionStart);
        Subscribe("https://push.example.com/phone");
        PlanWithMondayLift();
        _pushes.HasNudgeAsync(Joe, Today, PushNudgeKind.SessionStart, Arg.Any<CancellationToken>()).Returns(true);

        var sent = await Service().RunOnceAsync();

        sent.Should().Be(0);
        await _sender.DidNotReceive().SendAsync(Arg.Any<PushTarget>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
        await _pushes.DidNotReceive().AddNudgeAsync(Arg.Any<PushNudgeLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeSessionStart_OrOnARestDay_OrWhenDone_NothingGoesOut()
    {
        Subscribe("https://push.example.com/phone");
        var plan = PlanWithMondayLift();

        // Too early.
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 9, 16, 0, 0, TimeSpan.Zero));
        (await Service().RunOnceAsync()).Should().Be(0);

        // Session time, but the lift is already done.
        _time.SetUtcNow(MondaySessionStart);
        plan.CompleteSession(Today, SessionKind.Lift, CompletionSource.Manual, DayNumber.Day1);
        (await Service().RunOnceAsync()).Should().Be(0);

        // Tuesday has nothing scheduled.
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 17, 30, 0, TimeSpan.Zero));
        (await Service().RunOnceAsync()).Should().Be(0);

        await _sender.DidNotReceive().SendAsync(Arg.Any<PushTarget>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheEarliestDeviceTime_DrivesTheSessionNudge()
    {
        Subscribe("https://push.example.com/phone", new TimeOnly(17, 0));
        Subscribe("https://push.example.com/laptop", new TimeOnly(18, 30));
        PlanWithMondayLift();
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 9, 17, 5, 0, TimeSpan.Zero));

        var sent = await Service().RunOnceAsync();

        sent.Should().Be(1);
        await _sender.Received(2).SendAsync(Arg.Any<PushTarget>(), Arg.Is<PushMessage>(m => m.Body.StartsWith("Planned for 17:00")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task At2000_HabitsUnderMinimum_AreNamedWithValues()
    {
        Subscribe("https://push.example.com/phone");
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
        var walks = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 2, 1, null, 1);
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 2);
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition> { water, walks, creatine });
        _habits.GetLogsAsync(Joe, Today, Today, Arg.Any<CancellationToken>()).Returns(new List<HabitLog>
        {
            HabitLog.Create(water.Id, Joe, Today, 2, _time.GetUtcNow()),
            HabitLog.Create(creatine.Id, Joe, Today, 1, _time.GetUtcNow()),
        });
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 9, 20, 0, 0, TimeSpan.Zero));

        var sent = await Service().RunOnceAsync();

        sent.Should().Be(1);
        await _sender.Received(1).SendAsync(
            Arg.Any<PushTarget>(),
            Arg.Is<PushMessage>(m => m.Title == "Still under minimum" && m.Body == "Water 2/4, Walks 0/1" && m.Tag == "HabitsUnderMinimum"),
            Arg.Any<CancellationToken>());
        await _pushes.Received(1).AddNudgeAsync(Arg.Is<PushNudgeLog>(l => l.Kind == PushNudgeKind.HabitsUnderMinimum && l.LocalDate == Today), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task At2000_WithEverythingMet_NothingGoesOut_AndNothingIsLogged()
    {
        Subscribe("https://push.example.com/phone");
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition> { water });
        _habits.GetLogsAsync(Joe, Today, Today, Arg.Any<CancellationToken>()).Returns(new List<HabitLog> { HabitLog.Create(water.Id, Joe, Today, 5, _time.GetUtcNow()) });
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 9, 20, 10, 0, TimeSpan.Zero));

        (await Service().RunOnceAsync()).Should().Be(0);

        await _pushes.DidNotReceive().AddNudgeAsync(Arg.Any<PushNudgeLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AGoneSubscription_IsRemoved_ButTheNudgeStillCountsAsSent()
    {
        _time.SetUtcNow(MondaySessionStart);
        var dead = Subscribe("https://push.example.com/dead");
        var live = Subscribe("https://push.example.com/live");
        PlanWithMondayLift();
        _sender.SendAsync(Arg.Is<PushTarget>(t => t.Endpoint == dead.Endpoint), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>()).Returns(PushSendStatus.Gone);

        var sent = await Service().RunOnceAsync();

        sent.Should().Be(1);
        _pushes.Received(1).Remove(dead);
        _pushes.DidNotReceive().Remove(live);
        await _pushes.Received(1).AddNudgeAsync(Arg.Any<PushNudgeLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SummerTime_UsesLondonLocalTime()
    {
        // 16:30 UTC on 8 June 2026 is 17:30 BST in London: due. (Monday.)
        Subscribe("https://push.example.com/phone");
        var plan = TrainingPlan.Create(Joe, "Cut", new DateOnly(2026, 6, 1), new[] { ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A") }, 95m);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(plan);
        _time.SetUtcNow(new DateTimeOffset(2026, 6, 8, 16, 30, 0, TimeSpan.Zero));

        (await Service().RunOnceAsync()).Should().Be(1);
    }
}
