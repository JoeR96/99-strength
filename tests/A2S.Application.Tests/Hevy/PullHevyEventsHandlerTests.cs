using System.Text.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

/// <summary>
/// The pull end to end against recorded events, with the real CompleteDay and CompleteSession handlers
/// behind the MediatR sender so progression and plan completions are the real thing. Pins review
/// focus 1 (an event arriving twice makes one snapshot and one completion), 4 (a deletion keeps the
/// completion) and 5 (no key means Skipped, not failure), plus the AMRAP mapping and the cursor.
/// </summary>
public class PullHevyEventsHandlerTests
{
    private const string Key = "test";
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpperAUpdatedAt = new(2026, 11, 16, 18, 41, 12, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly LocalClock _clock;
    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly IHevyKeyResolver _keys = Substitute.For<IHevyKeyResolver>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly InMemoryLinks _links = new();
    private readonly InMemorySnapshots _snapshots = new();
    private readonly InMemoryCursors _cursors = new();
    private readonly HevyPullService _service;

    private readonly Workout _workout;
    private readonly TrainingPlan _plan;
    private readonly CardioProtocol _fourByFour = CardioProtocol.NorwegianFourByFour(Joe);

    public PullHevyEventsHandlerTests()
    {
        _clock = new LocalClock(_time);
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(Key);

        _workout = UpperA();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);
        _protocols.GetByIdAsync(_fourByFour.Id, Arg.Any<CancellationToken>()).Returns(_fourByFour);

        _plan = TrainingPlan.Create(Joe, CutTo85Nov2026.Name, Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, _fourByFour.Id, "Norwegian 4×4"),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);
        _plan.LinkWorkout(_workout.Id);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(_plan);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);

        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Routine, HevyLocalKeys.LiftRoutine(_plan.Id, Mon16Nov, DayNumber.Day1), "routine-upper-a", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "drill:open-books", "T-open-books", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "exercise:seal-row-barbell", "T-seal-row", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "cardio:NorwegianFourByFour", "T-4x4", Now));

        // The real handlers behind the sender, so A2S2 progression and plan completions actually happen.
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(Joe.Value);
        var completeDay = new CompleteDayCommandHandler(_workouts, _unitOfWork, currentUser);
        var completeSession = new CompleteSessionCommandHandler(_plans, _unitOfWork, currentUser, _clock, _protocols);
        _sender.Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeDay.Handle(call.Arg<CompleteDayCommand>(), CancellationToken.None));
        _sender.Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeSession.Handle(call.Arg<CompleteSessionCommand>(), CancellationToken.None));

        var library = Substitute.For<IExerciseLibraryProvider>();
        var catalogue = new HevyTemplateCatalogue(_hevy, _links, library, _unitOfWork, _clock);
        var completer = new HevySessionCompleter(_sender, _routines);
        _service = new HevyPullService(_hevy, _keys, _plans, _workouts, _links, _snapshots, _cursors, catalogue, completer, _unitOfWork, _clock, NullLogger<HevyPullService>.Instance);
    }

    private static Workout UpperA()
    {
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        var sealRow = Exercise.CreateWithRepsPerSetProgression("Seal Row (Barbell)", ExerciseCategory.Accessory, EquipmentType.Barbell,
            DayNumber.Day1, 2, "custom:seal-row-barbell", RepRange.Create(10, 10), 3, 5, false, Weight.Create(40m, WeightUnit.Kilograms));
        var squat = Exercise.CreateWithLinearProgression("Squat (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day2, 1, "D04AC939", TrainingMax.Create(140m, WeightUnit.Kilograms));
        var workout = new WorkoutBuilder().WithUserId(Joe).WithName(CutTo85Nov2026.Name).WithVariant(ProgramVariant.FourDay)
            .WithExercise(bench).WithExercise(sealRow).WithExercise(squat).Build();
        workout.Start();
        return workout;
    }

    private static List<HevyWorkoutEvent> Fixture(string name)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Hevy", "Fixtures", name));
        return JsonSerializer.Deserialize<List<HevyWorkoutEvent>>(json)!;
    }

    private void HevyReturns(params HevyWorkoutEvent[] events) =>
        _hevy.GetWorkoutEventsAsync(Key, Arg.Any<DateTimeOffset>(), 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkoutEvent>>.Success(new HevyPage<HevyWorkoutEvent> { Page = 1, PageCount = 1, Items = events }));

    [Fact]
    public async Task LinkedRoutineWorkout_CompletesTheA2S2Day_WithWorkingSetsAndAmrapOnTheLast_AndTheSession()
    {
        HevyReturns(Fixture("events-upper-a.json").ToArray());

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(new PullSummary(1, 1, 0, 0));

        var activity = _workout.CompletedActivities.Should().ContainSingle().Subject;
        activity.Day.Should().Be(DayNumber.Day1);
        activity.Performances.Should().HaveCount(2, "the warm-up drill is not an A2S2 exercise");
        var bench = activity.Performances.Single(p => p.ExerciseId == _workout.Exercises.Single(e => e.Name.StartsWith("Bench")).Id);
        bench.CompletedSets.Should().HaveCount(5, "the warm-up set is left out");
        bench.CompletedSets.SkipLast(1).Should().AllSatisfy(s => s.WasAmrap.Should().BeFalse());
        bench.CompletedSets[^1].WasAmrap.Should().BeTrue();
        bench.CompletedSets[^1].ActualReps.Should().Be(14);
        bench.CompletedSets[^1].Weight.Value.Should().Be(65m);
        var sealRow = activity.Performances.Single(p => p.ExerciseId != bench.ExerciseId);
        sealRow.CompletedSets.Should().HaveCount(3);
        sealRow.CompletedSets.Should().AllSatisfy(s => s.WasAmrap.Should().BeFalse("reps-per-set work has no AMRAP"));

        var completion = _plan.Completions.Should().ContainSingle().Subject;
        completion.Kind.Should().Be(SessionKind.Lift);
        completion.LiftDay.Should().Be(DayNumber.Day1);
        completion.Date.Should().Be(Mon16Nov);
        completion.Source.Should().Be(CompletionSource.Hevy);
        completion.HevyWorkoutId.Should().Be("b3c2a1d0-1111-4222-8333-444455556666");

        var snapshot = _snapshots.All.Should().ContainSingle().Subject;
        snapshot.Status.Should().Be(SnapshotStatus.Matched);
        snapshot.SessionCompletionId.Should().Be(completion.Id);
        snapshot.LocalDate.Should().Be(Mon16Nov);

        var cursor = _cursors.All.Should().ContainSingle().Subject;
        cursor.LastEventAtUtc.Should().Be(UpperAUpdatedAt);
        cursor.LastPulledAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task ReviewFocus1_TheSameEventTwice_MakesOneSnapshotAndOneCompletion()
    {
        var events = Fixture("events-upper-a.json");
        HevyReturns(events[0], events[0]);

        var first = await _service.PullAsync(Joe, force: false);
        _time.Advance(TimeSpan.FromMinutes(15));
        var second = await _service.PullAsync(Joe, force: false);

        first.Value.Matched.Should().Be(1);
        second.Value.Matched.Should().Be(0);
        _snapshots.All.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
        _workout.CompletedActivities.Should().ContainSingle();
        await _sender.Received(1).Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondPull_AsksHevyForEventsSinceTheNewestOneSeen()
    {
        HevyReturns(Fixture("events-upper-a.json").ToArray());
        await _service.PullAsync(Joe, force: false);

        _hevy.ClearReceivedCalls();
        HevyReturns();
        await _service.PullAsync(Joe, force: false);

        await _hevy.Received(1).GetWorkoutEventsAsync(Key, UpperAUpdatedAt, 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewFocus4_DeletedEventForAMatchedWorkout_FlagsTheSnapshot_AndKeepsTheCompletion()
    {
        HevyReturns(Fixture("events-upper-a.json").ToArray());
        await _service.PullAsync(Joe, force: false);
        HevyReturns(new HevyWorkoutEvent { Type = "deleted", Id = "b3c2a1d0-1111-4222-8333-444455556666", DeletedAt = Now.AddHours(1) });

        var result = await _service.PullAsync(Joe, force: false);

        result.Value.Pulled.Should().Be(1);
        _snapshots.All.Single().Status.Should().Be(SnapshotStatus.DeletedInHevy);
        _snapshots.All.Single().SessionCompletionId.Should().NotBeNull();
        _plan.Completions.Should().ContainSingle();
        _workout.CompletedActivities.Should().ContainSingle();
        _cursors.All.Single().LastEventAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public async Task ReviewFocus5_NoStoredKey_IsSkipped_NotAFailure()
    {
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.WasSkipped.Should().BeTrue();
        result.Value.SkippedReason.Should().Be("no Hevy key");
        await _hevy.DidNotReceiveWithAnyArgs().GetWorkoutEventsAsync(default!, default, default, default, default);
        _cursors.All.Should().BeEmpty();
    }

    [Fact]
    public async Task NoActivePlan_IsSkipped()
    {
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((TrainingPlan?)null);

        var result = await _service.PullAsync(Joe, force: false);

        result.Value.SkippedReason.Should().Be("no active plan");
    }

    [Fact]
    public async Task CardioByTemplate_CompletesWithTheProtocolsZoneMinutes_AndARestDayWorkoutGoesToTheInbox()
    {
        HevyReturns(Fixture("events-cardio-and-rest.json").ToArray());

        var result = await _service.PullAsync(Joe, force: false);

        result.Value.Should().Be(new PullSummary(2, 1, 1, 0));
        var cardio = _plan.Completions.Should().ContainSingle().Subject;
        cardio.Kind.Should().Be(SessionKind.Cardio);
        cardio.Date.Should().Be(new DateOnly(2026, 11, 18));
        cardio.CardioProtocolId.Should().Be(_fourByFour.Id);
        cardio.Cardio!.DurationSeconds.Should().Be(2400);
        cardio.Cardio.DistanceMeters.Should().Be(7000m);
        cardio.Cardio.Rpe.Should().Be(9);
        cardio.Cardio.HighIntensityMinutes.Should().Be(16, "the 4×4 default fills in when the watch did not say");

        var inbox = _snapshots.All.Single(s => s.Status == SnapshotStatus.Unmatched);
        inbox.Title.Should().Be("Bonus arms");
        inbox.Reason.Should().Be("rest day");
        inbox.LocalDate.Should().Be(new DateOnly(2026, 11, 22));
    }

    [Fact]
    public async Task HevyFailure_IsReturned_AndTheCursorDoesNotMove()
    {
        _hevy.GetWorkoutEventsAsync(Key, Arg.Any<DateTimeOffset>(), 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkoutEvent>>.Failure(HevyError.RateLimited(TimeSpan.FromSeconds(7))));

        var result = await _service.PullAsync(Joe, force: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Hevy is rate limiting, try again in 7 s");
        result.ErrorCode.Should().Be(ErrorCode.ExternalServiceError);
        var planStart = new DateTimeOffset(_clock.BoundsOf(Mon16Nov).StartUtc, TimeSpan.Zero);
        _cursors.All.Single().LastEventAtUtc.Should().Be(planStart);
    }

    [Fact]
    public async Task ASecondLiftForTheSameDayInTheWeek_LandsInTheInboxWithTheRuleAsReason()
    {
        var events = Fixture("events-upper-a.json");
        var again = JsonSerializer.Deserialize<HevyWorkout>(JsonSerializer.Serialize(events[0].Workout))! with
        {
            Id = "second-upper-a",
            StartTime = new DateTimeOffset(2026, 11, 17, 17, 30, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 11, 17, 18, 30, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 17, 18, 31, 0, TimeSpan.Zero)
        };
        HevyReturns(events[0], new HevyWorkoutEvent { Type = "updated", Workout = again });

        var result = await _service.PullAsync(Joe, force: false);

        result.Value.Matched.Should().Be(1);
        result.Value.Unmatched.Should().Be(1);
        _snapshots.All.Single(s => s.Id.Value == "second-upper-a").Reason.Should().Contain("already been completed in Week 1");
        _plan.Completions.Should().ContainSingle();
    }
}
