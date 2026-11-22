using A2S.Application.Cardio;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Mobility;
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
/// Gym mode end to end with the real CompleteDay and CompleteSession handlers behind the sender, the real
/// poster, pull and retry runner, and Hevy faked at the client. Pins brief § 3a: finish completes the day and
/// the plan slot and posts typed sets with real times; a replay completes nothing twice; post then pull gives
/// one completion and one progression; a failed post retries without ever creating a second Hevy workout
/// (look up, adopt); and a pull that sees the workout before the post recorded its id does not complete again.
/// </summary>
public class GymSessionTests
{
    private const string Key = "test";
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = new(2026, 11, 16, 17, 55, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = new(2026, 11, 16, 18, 58, 0, TimeSpan.Zero);
    private static readonly Guid ClientSession = Guid.Parse("9a9a9a9a-0000-0000-0000-000000000001");
    private const string Title = CutTo85Nov2026.Name + " · W1 Upper A";

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
    private readonly InMemoryGymPosts _posts = new();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly CardioTrack _stairTrack;

    private readonly FinishGymSessionCommandHandler _finish;
    private readonly HevyPullService _pull;
    private readonly HevyPostRetryRunner _retry;
    private readonly RetryHevyPostCommandHandler _retryCommand;

    private readonly Workout _workout;
    private readonly TrainingPlan _plan;
    private readonly MobilityRoutine _mobilityA;
    private readonly CardioProtocol _stairs;
    private readonly List<HevyWorkoutDraft> _posted = new();
    private readonly List<HevyWorkout> _inHevy = new();

    public GymSessionTests()
    {
        _clock = new LocalClock(_time);
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(Key);

        _workout = UpperA();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);

        _mobilityA = MobilityRoutine.Create(Joe, CutTo85Nov2026.MobilityAName, 10,
        [
            new MobilityDrill("Open Books", 1, DrillDose.Reps(8), SideBias.None, null, null, perSide: true),
            new MobilityDrill("Dead Hang", 2, DrillDose.Seconds(30), SideBias.None, null, null, perSide: false)
        ]);
        _routines.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns([_mobilityA]);
        _routines.GetByIdAsync(_mobilityA.Id, Arg.Any<CancellationToken>()).Returns(_mobilityA);

        _stairs = CardioProtocol.Liss(Joe, 30, "Stair finisher");
        _protocols.GetByIdAsync(_stairs.Id, Arg.Any<CancellationToken>()).Returns(_stairs);

        _stairTrack = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Mon16Nov);
        _tracks.GetByIdAsync(_stairTrack.Id, Arg.Any<CancellationToken>()).Returns(_stairTrack);
        _tracks.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns([_stairTrack]);

        _plan = TrainingPlan.Create(Joe, CutTo85Nov2026.Name, Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", finisher: new SessionFinisher(_stairs.Id)),
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day1, "Upper B", finisher: SessionFinisher.ForTrack(_stairTrack.Id)),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);
        _plan.LinkWorkout(_workout.Id);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(_plan);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);

        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "drill:open-books", "T-open-books", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "drill:dead-hang", "T-dead-hang", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "exercise:seal-row-barbell", "T-seal-row", Now));

        HevyAcceptsPosts();
        HevyListsWorkouts();
        _hevy.GetWorkoutEventsAsync(Key, Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => HevyResult<HevyPage<HevyWorkoutEvent>>.Success(new HevyPage<HevyWorkoutEvent>
            {
                Page = 1,
                PageCount = 1,
                Items = _inHevy.Select(w => new HevyWorkoutEvent { Type = "updated", Workout = w }).ToList()
            }));

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(Joe.Value);
        var completeDay = new CompleteDayCommandHandler(_workouts, _unitOfWork, currentUser);
        var completeSession = new CompleteSessionCommandHandler(_plans, _unitOfWork, currentUser, _clock, _protocols);
        _sender.Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeDay.Handle(call.Arg<CompleteDayCommand>(), CancellationToken.None));
        _sender.Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeSession.Handle(call.Arg<CompleteSessionCommand>(), CancellationToken.None));

        var catalogue = new HevyTemplateCatalogue(_hevy, _links, Substitute.For<IExerciseLibraryProvider>(), _unitOfWork, _clock);
        var poster = new HevyWorkoutPoster(_hevy, _keys, _posts, _plans, _workouts, _routines, _protocols, _links, catalogue, _unitOfWork, _clock,
            NullLogger<HevyWorkoutPoster>.Instance, _tracks);
        var recorder = new CardioTrackSessionRecorder(_tracks, Substitute.For<IUserRepository>(), Substitute.For<IHabitRepository>());
        _finish = new FinishGymSessionCommandHandler(_sender, _workouts, _plans, _routines, _posts, _keys, poster, _unitOfWork, currentUser, _clock,
            NullLogger<FinishGymSessionCommandHandler>.Instance, recorder);
        _pull = new HevyPullService(_hevy, _keys, _plans, _workouts, _links, _snapshots, _cursors, catalogue, new HevySessionCompleter(_sender, _routines),
            _posts, poster, _unitOfWork, _clock, NullLogger<HevyPullService>.Instance);
        _retry = new HevyPostRetryRunner(_posts, poster, _clock, NullLogger<HevyPostRetryRunner>.Instance);
        _retryCommand = new RetryHevyPostCommandHandler(_posts, poster, _unitOfWork, currentUser, _clock);
    }

    private static Workout UpperA()
    {
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        var sealRow = Exercise.CreateWithRepsPerSetProgression("Seal Row (Barbell)", ExerciseCategory.Accessory, EquipmentType.Barbell,
            DayNumber.Day1, 2, "custom:seal-row-barbell", RepRange.Create(10, 10), 3, 5, false, Weight.Create(40m, WeightUnit.Kilograms));
        var workout = new WorkoutBuilder().WithUserId(Joe).WithName(CutTo85Nov2026.Name).WithVariant(ProgramVariant.FourDay)
            .WithExercise(bench).WithExercise(sealRow).Build();
        workout.Start();
        return workout;
    }

    private Exercise Bench => _workout.Exercises.Single(e => e.Name.StartsWith("Bench", StringComparison.Ordinal));
    private Exercise SealRow => _workout.Exercises.Single(e => e.Name.StartsWith("Seal", StringComparison.Ordinal));
    private decimal BenchTm => ((LinearProgressionStrategy)Bench.Progression).TrainingMax.Value;

    private Queue<Func<HevyWorkoutDraft, HevyResult<HevyWorkout>>> CreateScript { get; } = new();

    /// <summary>By default Hevy creates the workout and lists it afterwards; tests can script failures first.</summary>
    private void HevyAcceptsPosts() =>
        _hevy.CreateWorkoutAsync(Key, Arg.Any<HevyWorkoutDraft>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var draft = call.Arg<HevyWorkoutDraft>();
                _posted.Add(draft);
                if (CreateScript.Count > 0)
                {
                    return CreateScript.Dequeue()(draft);
                }

                return HevyResult<HevyWorkout>.Success(Store(draft, $"hevy-{_posted.Count}"));
            });

    private void HevyListsWorkouts() =>
        _hevy.GetWorkoutsAsync(Key, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyPage<HevyWorkout>>.Success(new HevyPage<HevyWorkout>
            {
                Page = call.Arg<int>(),
                PageCount = 1,
                Items = call.Arg<int>() == 1 ? _inHevy.OrderByDescending(w => w.StartTime).ToList() : []
            }));

    /// <summary>What Hevy would hold after accepting the draft: the same exercises and sets, with an id.</summary>
    private HevyWorkout Store(HevyWorkoutDraft draft, string id)
    {
        var workout = new HevyWorkout
        {
            Id = id,
            Title = draft.Title,
            StartTime = draft.StartTime,
            EndTime = draft.EndTime,
            UpdatedAt = draft.EndTime.AddMinutes(1),
            Exercises = draft.Exercises.Select((e, i) => new HevyWorkoutExercise
            {
                Index = i,
                ExerciseTemplateId = e.ExerciseTemplateId,
                Sets = e.Sets.Select((s, j) => new HevyWorkoutSet { Index = j, Type = s.Type, WeightKg = s.WeightKg, Reps = s.Reps, DurationSeconds = s.DurationSeconds }).ToList()
            }).ToList()
        };
        _inHevy.Add(workout);
        return workout;
    }

    private List<ExercisePerformanceRequest> AsPlanned(int amrapReps, bool includeSealRow = true)
    {
        var week = _workout.GetTemplateWeek(_workout.CurrentWeek);
        var block = _workout.GetBlockType(_workout.CurrentWeek);
        var exercises = includeSealRow ? new[] { Bench, SealRow } : new[] { Bench };
        return exercises.Select(e => new ExercisePerformanceRequest
        {
            ExerciseId = e.Id.Value,
            CompletedSets = e.CalculatePlannedSets(week, block).Select(p => new CompletedSetRequest
            {
                SetNumber = p.SetNumber,
                Weight = p.Weight.Value,
                WeightUnit = p.Weight.Unit,
                ActualReps = p.IsAmrap ? amrapReps : p.TargetReps,
                WasAmrap = p.IsAmrap
            }).ToList()
        }).ToList();
    }

    private FinishGymSessionCommand Finish(List<ExercisePerformanceRequest>? performances = null, FinisherLogRequest? finisher = null) =>
        new(ClientSession, _workout.Id.Value, DayNumber.Day1, Started, Ended, performances ?? AsPlanned(amrapReps: 14),
            WarmUpTicks: [true, false], Finisher: finisher);

    [Fact]
    public async Task Finish_CompletesTheDayAndTheSlot_AndPostsTypedSetsWithRealTimes()
    {
        var result = await _finish.Handle(Finish(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.HevyStatus.Should().Be("Posted");
        result.Value.HevyWorkoutId.Should().Be("hevy-1");
        result.Value.CompleteDay!.ProgressionChanges.Should().HaveCount(2);

        _workout.CompletedActivities.Should().ContainSingle();
        var completion = _plan.Completions.Should().ContainSingle().Subject;
        completion.Kind.Should().Be(SessionKind.Lift);
        completion.Date.Should().Be(Mon16Nov);
        completion.Source.Should().Be(CompletionSource.Manual);
        completion.HevyWorkoutId.Should().Be("hevy-1");
        result.Value.CompletionId.Should().Be(completion.Id.Value);

        var draft = _posted.Should().ContainSingle().Subject;
        draft.Title.Should().Be(Title);
        draft.StartTime.Should().Be(Started);
        draft.EndTime.Should().Be(Ended);
        draft.Exercises.Select(e => e.ExerciseTemplateId).Should().Equal("T-open-books", "79D0BB3A", "T-seal-row");
        draft.Exercises[0].Sets.Should().HaveCount(2, "one set per side; the unticked drill is left out").And.AllSatisfy(s => s.Type.Should().Be(HevySetTypes.Warmup));
        draft.Exercises[1].Sets.SkipLast(1).Should().AllSatisfy(s => s.Type.Should().Be(HevySetTypes.Normal));
        draft.Exercises[1].Sets[^1].Type.Should().Be(HevySetTypes.Failure);
        draft.Exercises[1].Sets[^1].Reps.Should().Be(14);
        draft.Exercises[2].Sets.Should().AllSatisfy(s => s.Type.Should().Be(HevySetTypes.Normal));

        var post = _posts.All.Should().ContainSingle().Subject;
        post.Status.Should().Be(HevyPostStatus.Posted);
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Workout && l.LocalKey == HevyLocalKeys.GymSession(ClientSession) && l.HevyId == "hevy-1");
    }

    [Fact]
    public async Task Finish_Replayed_ReturnsTheFirstResult_AndNeverCompletesOrPostsTwice()
    {
        var first = await _finish.Handle(Finish(), CancellationToken.None);
        var second = await _finish.Handle(Finish(), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.Replayed.Should().BeTrue();
        second.Value.CompletionId.Should().Be(first.Value.CompletionId);
        second.Value.HevyWorkoutId.Should().Be("hevy-1");
        second.Value.CompleteDay!.ProgressionChanges.Should().BeEquivalentTo(first.Value.CompleteDay!.ProgressionChanges);
        _workout.CompletedActivities.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
        _posted.Should().ContainSingle();
        await _sender.Received(1).Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finish_FromAnotherDevice_ForADayAlreadyFinished_IsAConflict_AndChangesNothing()
    {
        await _finish.Handle(Finish(), CancellationToken.None);
        var tmAfterFirst = BenchTm;
        var otherDevice = Finish() with { ClientSessionId = Guid.Parse("9a9a9a9a-0000-0000-0000-000000000002") };

        var second = await _finish.Handle(otherDevice, CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.ErrorCode.Should().Be(ErrorCode.Conflict);
        second.Error.Should().Be("This day was already finished on another device");
        BenchTm.Should().Be(tmAfterFirst);
        _workout.CompletedActivities.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
        _posts.All.Should().ContainSingle();
        _posted.Should().ContainSingle();
        await _sender.Received(1).Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostThenPull_GivesExactlyOneCompletionAndOneProgression()
    {
        await _finish.Handle(Finish(), CancellationToken.None);
        var tmAfterFinish = BenchTm;
        _time.Advance(TimeSpan.FromMinutes(15));

        var pull = await _pull.PullAsync(Joe, force: false);

        pull.IsSuccess.Should().BeTrue(pull.Error);
        pull.Value.Matched.Should().Be(0);
        tmAfterFinish.Should().BeGreaterThan(100m, "a strong AMRAP raises the TM once");
        BenchTm.Should().Be(tmAfterFinish);
        _workout.CompletedActivities.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
        _snapshots.All.Single().Status.Should().Be(SnapshotStatus.Matched);
        _snapshots.All.Single().SessionCompletionId.Should().Be(_plan.Completions.Single().Id);
    }

    [Fact]
    public async Task HevyDown_SessionIsStillComplete_AndTheRetryAdoptsTheWorkoutATimeoutLeftInHevy()
    {
        // The first POST reaches Hevy but the answer is lost (a timeout): the workout exists, we were told "unavailable".
        CreateScript.Enqueue(draft =>
        {
            Store(draft, "hevy-made-anyway");
            return HevyResult<HevyWorkout>.Failure(HevyError.Unavailable());
        });

        var result = await _finish.Handle(Finish(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HevyStatus.Should().Be("Pending");
        _workout.CompletedActivities.Should().ContainSingle();
        _plan.Completions.Single().HevyWorkoutId.Should().BeNull();
        var post = _posts.All.Single();
        post.Attempts.Should().Be(1);

        _time.Advance(TimeSpan.FromMinutes(1));
        (await _retry.RunOnceAsync()).Should().Be(0, "the 2-minute backoff has not elapsed");
        _time.Advance(TimeSpan.FromMinutes(2));
        (await _retry.RunOnceAsync()).Should().Be(1);

        _posted.Should().ContainSingle("the retry found the workout and adopted it instead of posting again");
        post.Status.Should().Be(HevyPostStatus.Posted);
        post.HevyWorkoutId.Should().Be("hevy-made-anyway");
        _plan.Completions.Single().HevyWorkoutId.Should().Be("hevy-made-anyway");
        _inHevy.Should().ContainSingle();
    }

    [Fact]
    public async Task HevyDown_TheRetryPostsOnce_WhenTheWorkoutIsNotThere()
    {
        CreateScript.Enqueue(_ =>
        {
            _inHevy.Clear();
            return HevyResult<HevyWorkout>.Failure(HevyError.Unavailable());
        });
        await _finish.Handle(Finish(), CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(3));

        await _retry.RunOnceAsync();
        _time.Advance(TimeSpan.FromHours(1));
        await _retry.RunOnceAsync();

        _posted.Should().HaveCount(2, "one failed attempt, one successful retry, nothing after");
        _inHevy.Should().ContainSingle();
        _posts.All.Single().Status.Should().Be(HevyPostStatus.Posted);
        _plan.Completions.Single().HevyWorkoutId.Should().Be(_inHevy.Single().Id);
    }

    [Fact]
    public async Task PullSeesTheWorkoutBeforeThePostRecordedItsId_AndDoesNotCompleteAgain()
    {
        CreateScript.Enqueue(draft =>
        {
            Store(draft, "hevy-raced");
            return HevyResult<HevyWorkout>.Failure(HevyError.Unavailable());
        });
        await _finish.Handle(Finish(), CancellationToken.None);
        var tmAfterFinish = BenchTm;

        var pull = await _pull.PullAsync(Joe, force: false);
        _time.Advance(TimeSpan.FromHours(1));
        await _retry.RunOnceAsync();

        pull.IsSuccess.Should().BeTrue(pull.Error);
        _workout.CompletedActivities.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
        BenchTm.Should().Be(tmAfterFinish);
        _posts.All.Single().Status.Should().Be(HevyPostStatus.Posted);
        _posts.All.Single().HevyWorkoutId.Should().Be("hevy-raced");
        _plan.Completions.Single().HevyWorkoutId.Should().Be("hevy-raced");
        _posted.Should().ContainSingle();
        await _sender.Received(1).Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finisher_RecordsTheCardioCompletion_AndPostsStairMachineFloors()
    {
        var result = await _finish.Handle(Finish(finisher: new FinisherLogRequest(600, Effort.Solid, Level: 8, Floors: 42)), CancellationToken.None);

        result.Value.FinisherCompletionId.Should().NotBeNull();
        var cardio = _plan.Completions.Single(c => c.Kind == SessionKind.Cardio);
        cardio.CardioProtocolId.Should().Be(_stairs.Id);
        cardio.Cardio!.DurationSeconds.Should().Be(600);
        cardio.Cardio.Rpe.Should().Be(7);
        cardio.Cardio.Notes.Should().BeNull("level, floors and effort are fields on the cardio log now");
        cardio.Cardio.Floors.Should().Be(42);
        cardio.Cardio.IntensityUnit.Should().Be(IntensityUnit.Level);
        cardio.Cardio.IntensityValue.Should().Be(8);
        cardio.Cardio.Effort.Should().Be(EffortRating.Solid);

        var stair = _posted.Single().Exercises[^1];
        stair.ExerciseTemplateId.Should().Be(HevyWorkoutPoster.StairMachineFloorsTemplateId);
        stair.Sets.Single().DurationSeconds.Should().Be(600);
        stair.Sets.Single().CustomMetric.Should().Be(42m);
        stair.Sets.Single().Rpe.Should().Be(7m);
        stair.Notes.Should().Be("Level 8 · Solid");
    }

    [Fact]
    public async Task TrackFinisher_IsCompletedAndEvaluatedOnce_AndPostedAsStairMachineFloors()
    {
        var thursday = new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);
        var command = new FinishGymSessionCommand(ClientSession, _workout.Id.Value, DayNumber.Day1, thursday, thursday.AddHours(1), AsPlanned(amrapReps: 12),
            Finisher: new FinisherLogRequest(300, Effort.Brutal, Level: 6, Floors: 31, Pain: PainFlag.Niggle, PainArea: "left knee"));

        var first = await _finish.Handle(command, CancellationToken.None);
        var replay = await _finish.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue(first.Error);
        replay.Value.Replayed.Should().BeTrue();
        var cardio = _plan.Completions.Single(c => c.Kind == SessionKind.Cardio);
        cardio.CardioTrackId.Should().Be(_stairTrack.Id);
        cardio.CardioProtocolId.Should().BeNull();
        cardio.Cardio!.Pain.Should().Be(PainFlag.Niggle);
        cardio.Cardio.PainArea.Should().Be("left knee");

        var session = _stairTrack.Sessions.Should().ContainSingle().Subject;
        session.Key.Should().Be(cardio.TrackSessionKey);
        session.Floors.Should().Be(31);
        session.Outcome.Should().Be(TrackOutcome.Held, "a niggle never counts towards the next step");

        var stair = _posted.Should().ContainSingle().Subject.Exercises[^1];
        stair.ExerciseTemplateId.Should().Be(HevyTemplateCatalogue.StairMachineFloors);
        stair.Sets.Single().CustomMetric.Should().Be(31m);
        stair.Notes.Should().Be("Stair finisher · Level 6 · Brutal");
    }

    [Fact]
    public async Task FinishEarly_PostsOnlyWhatWasDone_AndLeavesTheSkippedExerciseUntouched()
    {
        var sealRowBefore = ((RepsPerSetStrategy)SealRow.Progression).CurrentSetCount;

        await _finish.Handle(Finish(AsPlanned(amrapReps: 10, includeSealRow: false)), CancellationToken.None);

        _workout.CompletedActivities.Single().Performances.Should().ContainSingle();
        ((RepsPerSetStrategy)SealRow.Progression).CurrentSetCount.Should().Be(sealRowBefore);
        _posted.Single().Exercises.Select(e => e.ExerciseTemplateId).Should().NotContain("T-seal-row");
    }

    [Fact]
    public async Task NoHevyKey_CompletesTheSession_AndSkipsThePost()
    {
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await _finish.Handle(Finish(), CancellationToken.None);

        result.Value.HevyStatus.Should().Be("Skipped");
        _workout.CompletedActivities.Should().ContainSingle();
        _posted.Should().BeEmpty();
        (await _retry.RunOnceAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteDayRefuses_NothingOfTheFinishIsKept()
    {
        var unknown = new List<ExercisePerformanceRequest>
        {
            new() { ExerciseId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"), CompletedSets = [] }
        };

        var result = await _finish.Handle(Finish(unknown), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _plan.Completions.Should().BeEmpty();
        _posts.All.Should().BeEmpty();
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task RetryCommand_RequeuesAFailedPost_AndSendsIt()
    {
        CreateScript.Enqueue(_ => HevyResult<HevyWorkout>.Failure(HevyError.InvalidKey()));
        var finished = await _finish.Handle(Finish(), CancellationToken.None);
        finished.Value.HevyStatus.Should().Be("Failed");
        _inHevy.Clear();

        var retried = await _retryCommand.Handle(new RetryHevyPostCommand(ClientSession), CancellationToken.None);

        retried.Value.Status.Should().Be("Posted");
        _inHevy.Should().ContainSingle();
    }
}
