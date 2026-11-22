using System.Text.Json;
using A2S.Application.Cardio;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
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
using Xunit;

namespace A2S.Application.Tests.Hevy;

/// <summary>
/// Gym-mode reconciliation and brief § 3a session posts: a lift whose slot has a track finisher completes the
/// finisher slot from a Hevy pull (Today shows it done) and evaluates the track once; a walk logged in the app
/// is posted to Hevy from the server, and pulling that workout back changes nothing.
/// </summary>
public class CardioTrackFinisherAndSessionPostTests
{
    private const string Key = "test";
    private const string StairWorkoutId = "d4d4d4d4-3333-4444-8555-666677778888";
    private static readonly UserId Joe = new("user_joe_cardio");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 19, 0, 0, TimeSpan.Zero);

    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly IHevyKeyResolver _keys = Substitute.For<IHevyKeyResolver>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly InMemoryLinks _links = new();
    private readonly InMemorySnapshots _snapshots = new();
    private readonly InMemoryCursors _cursors = new();
    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));
    private readonly Workout _workout;
    private readonly TrainingPlan _plan;
    private readonly CardioTrack _stairs;
    private readonly CardioTrack _walk;
    private readonly HabitDefinition _walksHabit;
    private readonly HevyPullService _service;
    private readonly CompleteSessionCommandHandler _completeSession;
    private readonly InMemoryGymPosts _posts = new();
    private readonly List<HevyWorkoutDraft> _posted = new();
    private readonly List<HevyWorkout> _inHevy = new();

    public CardioTrackFinisherAndSessionPostTests()
    {
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(Key);

        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        _workout = new WorkoutBuilder().WithUserId(Joe).WithName(CutTo85Nov2026.Name).WithVariant(ProgramVariant.FourDay)
            .WithExercise(bench).Build();
        _workout.Start();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);

        _stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Mon16Nov);
        _walk = CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Joe, Mon16Nov);
        _tracks.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new[] { _stairs, _walk });
        _tracks.GetByIdAsync(_stairs.Id, Arg.Any<CancellationToken>()).Returns(_stairs);
        _tracks.GetByIdAsync(_walk.Id, Arg.Any<CancellationToken>()).Returns(_walk);
        _walksHabit = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 5, 3, null, 1);
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>()).Returns(new[] { _walksHabit });

        _plan = TrainingPlan.Create(Joe, CutTo85Nov2026.Name, Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", finisher: SessionFinisher.ForTrack(_stairs.Id)),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, null, "Walk", _walk.Id),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);
        _plan.LinkWorkout(_workout.Id);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(_plan);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Routine, HevyLocalKeys.LiftRoutine(_plan.Id, Mon16Nov, DayNumber.Day1), "routine-upper-a", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "drill:open-books", "T-open-books", Now));

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(Joe.Value);
        var recorder = new CardioTrackSessionRecorder(_tracks, _users, _habits);
        var completeDay = new CompleteDayCommandHandler(_workouts, _unitOfWork, currentUser);
        var catalogue = new HevyTemplateCatalogue(_hevy, _links, Substitute.For<IExerciseLibraryProvider>(), _unitOfWork, _clock);
        var poster = new HevyWorkoutPoster(_hevy, _keys, _posts, _plans, _workouts, _routines, _protocols, _links, catalogue, _unitOfWork, _clock,
            NullLogger<HevyWorkoutPoster>.Instance, _tracks);
        var sessionPoster = new SessionHevyPoster(_posts, _keys, poster, _routines, _protocols, _tracks, _clock, NullLogger<SessionHevyPoster>.Instance);
        var completeSession = new CompleteSessionCommandHandler(_plans, _unitOfWork, currentUser, _clock, _protocols, recorder, sessionPoster);
        _completeSession = completeSession;
        _hevy.CreateWorkoutAsync(Key, Arg.Any<HevyWorkoutDraft>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var draft = call.Arg<HevyWorkoutDraft>();
            _posted.Add(draft);
            var stored = new HevyWorkout
            {
                Id = $"hevy-posted-{_posted.Count}",
                Title = draft.Title,
                StartTime = draft.StartTime,
                EndTime = draft.EndTime,
                UpdatedAt = draft.EndTime.AddMinutes(1),
                Exercises = draft.Exercises.Select((e, i) => new HevyWorkoutExercise
                {
                    Index = i,
                    ExerciseTemplateId = e.ExerciseTemplateId,
                    Sets = e.Sets.Select((s, j) => new HevyWorkoutSet { Index = j, DurationSeconds = s.DurationSeconds, DistanceMeters = s.DistanceMeters }).ToList()
                }).ToList()
            };
            _inHevy.Add(stored);
            return HevyResult<HevyWorkout>.Success(stored);
        });
        _hevy.GetWorkoutsAsync(Key, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyPage<HevyWorkout>>.Success(new HevyPage<HevyWorkout> { Page = 1, PageCount = 1, Items = _inHevy.ToList() }));
        _sender.Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeDay.Handle(call.Arg<CompleteDayCommand>(), CancellationToken.None));
        _sender.Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeSession.Handle(call.Arg<CompleteSessionCommand>(), CancellationToken.None));

        var completer = new HevySessionCompleter(_sender, _routines, new HevyCardioTrackLogger(_tracks, recorder, _clock));
        _service = new HevyPullService(_hevy, _keys, _plans, _workouts, _links, _snapshots, _cursors, catalogue, completer, _posts, poster, _unitOfWork,
            _clock, NullLogger<HevyPullService>.Instance);
    }

    private static HevyWorkoutEvent[] Fixture(string name) =>
        JsonSerializer.Deserialize<List<HevyWorkoutEvent>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Hevy", "Fixtures", name)))!.ToArray();

    private void HevyReturns(params HevyWorkoutEvent[] events) =>
        _hevy.GetWorkoutEventsAsync(Key, Arg.Any<DateTimeOffset>(), 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkoutEvent>>.Success(new HevyPage<HevyWorkoutEvent> { Page = 1, PageCount = 1, Items = events }));

    [Fact]
    public async Task LiftWithATrackFinisher_FromHevy_CompletesTheFinisherSlot_AndEvaluatesTheTrackOnce()
    {
        var events = Fixture("events-upper-a-stair-finisher.json");
        HevyReturns(events[0], events[0]);

        await _service.PullAsync(Joe, force: false);
        await _service.PullAsync(Joe, force: true);

        _plan.Completions.Should().HaveCount(2);
        var finisher = _plan.Completions.Single(c => c.Kind == SessionKind.Cardio);
        finisher.CardioTrackId.Should().Be(_stairs.Id);
        finisher.Date.Should().Be(Mon16Nov);
        finisher.Cardio!.Floors.Should().Be(32);
        var session = _stairs.Sessions.Should().ContainSingle().Subject;
        session.Key.Should().Be(finisher.TrackSessionKey);
        _posted.Should().BeEmpty("a session that came from Hevy is never posted back");
    }

    [Fact]
    public async Task WalkLoggedInTheApp_IsPostedToHevy_AndPullingItBackChangesNothing()
    {
        var wednesday = Mon16Nov.AddDays(2);
        var result = await _completeSession.Handle(new CompleteSessionCommand(_plan.Id.Value, wednesday, SessionKind.Cardio,
            Cardio: new CardioLogDto(1500, null, null, 2100m, null, Effort: EffortRating.Easy)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var draft = _posted.Should().ContainSingle().Subject;
        draft.Title.Should().Be(CutTo85Nov2026.Name + " · Walk");
        draft.Exercises.Should().ContainSingle().Which.ExerciseTemplateId.Should().Be(HevyTemplateCatalogue.Treadmill);
        draft.Exercises[0].Sets.Single().DurationSeconds.Should().Be(1500);
        draft.Exercises[0].Sets.Single().DistanceMeters.Should().Be(2100);
        draft.EndTime.Should().Be(Now);
        draft.StartTime.Should().Be(Now.AddSeconds(-1500));
        _posts.All.Single().Status.Should().Be(HevyPostStatus.Posted);

        HevyReturns(_inHevy.Select(w => new HevyWorkoutEvent { Type = "updated", Workout = w }).ToArray());
        await _service.PullAsync(Joe, force: true);

        _plan.Completions.Should().ContainSingle();
        _walk.Sessions.Should().ContainSingle();
        _posted.Should().ContainSingle();
    }

    [Fact]
    public async Task WalkLoggedTwice_PostsOnce()
    {
        var command = new CompleteSessionCommand(_plan.Id.Value, Mon16Nov.AddDays(2), SessionKind.Cardio,
            Cardio: new CardioLogDto(1500, null, null, null, null, Effort: EffortRating.Easy));

        await _completeSession.Handle(command, CancellationToken.None);
        await _completeSession.Handle(command, CancellationToken.None);

        _posts.All.Should().ContainSingle();
        _posted.Should().ContainSingle();
    }

    [Fact]
    public async Task HomeRoutinePlayed_IsPostedAsYoga_WithTheDrillsDoneInTheNotes()
    {
        var yoga = A2S.Domain.Aggregates.Mobility.MobilityRoutine.Create(Joe, "Evening yoga", 15,
        [
            new A2S.Domain.Aggregates.Mobility.MobilityDrill("Child's pose", 1, A2S.Domain.Aggregates.Mobility.DrillDose.Seconds(60),
                A2S.Domain.Aggregates.Mobility.SideBias.None, null, null, false),
            new A2S.Domain.Aggregates.Mobility.MobilityDrill("Pigeon", 1, A2S.Domain.Aggregates.Mobility.DrillDose.Seconds(60),
                A2S.Domain.Aggregates.Mobility.SideBias.None, null, null, true)
        ], A2S.Domain.Aggregates.Mobility.MobilityRoutineKind.YogaFlow);
        _routines.GetByIdAsync(yoga.Id, Arg.Any<CancellationToken>()).Returns(yoga);

        await _completeSession.Handle(new CompleteSessionCommand(_plan.Id.Value, Mon16Nov.AddDays(1), SessionKind.Mobility,
            MobilityRoutineId: yoga.Id.Value, DrillTicks: [true, false]), CancellationToken.None);

        var exercise = _posted.Should().ContainSingle().Subject.Exercises.Should().ContainSingle().Subject;
        exercise.ExerciseTemplateId.Should().Be(HevyTemplateCatalogue.Yoga);
        exercise.Notes.Should().Be("Evening yoga: Child's pose");
        exercise.Sets.Single().DurationSeconds.Should().Be(15 * 60);
    }

    [Fact]
    public async Task StretchRoutine_IsPostedAsStretching_EvenWithYogaInTheName()
    {
        var stretch = A2S.Domain.Aggregates.Mobility.MobilityRoutine.Create(Joe, "Yoga-ish stretch", 15,
        [
            new A2S.Domain.Aggregates.Mobility.MobilityDrill("Pigeon", 1, A2S.Domain.Aggregates.Mobility.DrillDose.Seconds(60),
                A2S.Domain.Aggregates.Mobility.SideBias.None, null, null, true)
        ], A2S.Domain.Aggregates.Mobility.MobilityRoutineKind.Stretch);
        _routines.GetByIdAsync(stretch.Id, Arg.Any<CancellationToken>()).Returns(stretch);

        await _completeSession.Handle(new CompleteSessionCommand(_plan.Id.Value, Mon16Nov.AddDays(1), SessionKind.Mobility,
            MobilityRoutineId: stretch.Id.Value), CancellationToken.None);

        _posted.Should().ContainSingle().Subject.Exercises.Single().ExerciseTemplateId.Should().Be(HevyTemplateCatalogue.Stretching);
    }

    [Fact]
    public async Task FollowAlong_IsPostedAsYoga_ForTheMinutesActuallyDone()
    {
        var video = A2S.Domain.Aggregates.Mobility.MobilityRoutine.Create(Joe, "Hip flow video", 25, [],
            A2S.Domain.Aggregates.Mobility.MobilityRoutineKind.FollowAlong, "https://example.com/flow");
        _routines.GetByIdAsync(video.Id, Arg.Any<CancellationToken>()).Returns(video);

        var result = await _completeSession.Handle(new CompleteSessionCommand(_plan.Id.Value, Mon16Nov.AddDays(1), SessionKind.Mobility,
            MobilityRoutineId: video.Id.Value, DurationMinutes: 32), CancellationToken.None);

        result.Value.DurationMinutes.Should().Be(32);
        var exercise = _posted.Should().ContainSingle().Subject.Exercises.Single();
        exercise.ExerciseTemplateId.Should().Be(HevyTemplateCatalogue.Yoga);
        exercise.Notes.Should().Be("Hip flow video");
        exercise.Sets.Single().DurationSeconds.Should().Be(32 * 60);
    }
}
