using System.Text.Json;
using A2S.Application.Cardio;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
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
using A2S.Domain.Aggregates.Workout.Cardio;
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
/// Brief § 4e pull, against recorded fixtures with the real handlers behind the sender: a stair
/// finisher inside a lift workout evaluates the stair track once (dedupe, re-pull, edits), and a
/// walking-only workout completes the cardio slot that points at the walk track and evaluates it once.
/// </summary>
public class CardioTrackHevyPullTests
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

    public CardioTrackHevyPullTests()
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
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
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
        var completeSession = new CompleteSessionCommandHandler(_plans, _unitOfWork, currentUser, _clock, _protocols, recorder);
        _sender.Send(Arg.Any<CompleteDayCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeDay.Handle(call.Arg<CompleteDayCommand>(), CancellationToken.None));
        _sender.Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeSession.Handle(call.Arg<CompleteSessionCommand>(), CancellationToken.None));

        var catalogue = new HevyTemplateCatalogue(_hevy, _links, Substitute.For<IExerciseLibraryProvider>(), _unitOfWork, _clock);
        var completer = new HevySessionCompleter(_sender, _routines, new HevyCardioTrackLogger(_tracks, recorder, _clock));
        var posts = new InMemoryGymPosts();
        var poster = new HevyWorkoutPoster(_hevy, _keys, posts, _plans, _workouts, _routines, _protocols, _links, catalogue, _unitOfWork, _clock,
            NullLogger<HevyWorkoutPoster>.Instance);
        _service = new HevyPullService(_hevy, _keys, _plans, _workouts, _links, _snapshots, _cursors, catalogue, completer, posts, poster, _unitOfWork,
            _clock, NullLogger<HevyPullService>.Instance);
    }

    private static HevyWorkoutEvent[] Fixture(string name) =>
        JsonSerializer.Deserialize<List<HevyWorkoutEvent>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Hevy", "Fixtures", name)))!.ToArray();

    private void HevyReturns(params HevyWorkoutEvent[] events) =>
        _hevy.GetWorkoutEventsAsync(Key, Arg.Any<DateTimeOffset>(), 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkoutEvent>>.Success(new HevyPage<HevyWorkoutEvent> { Page = 1, PageCount = 1, Items = events }));

    [Fact]
    public async Task StairFinisherInLiftWorkout_CompletesTheLift_AndEvaluatesTheStairTrackOnce()
    {
        HevyReturns(Fixture("events-upper-a-stair-finisher.json"));

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        _workout.CompletedActivities.Should().ContainSingle();
        var session = _stairs.Sessions.Should().ContainSingle().Subject;
        session.Key.Should().Be($"hevy:{StairWorkoutId}:3");
        session.Floors.Should().Be(32);
        session.DurationSeconds.Should().Be(300);
        session.Rpe.Should().Be(9);
        session.Source.Should().Be(CardioLogSource.Hevy);
        session.Date.Should().Be(Mon16Nov);
        _stairs.State.Qualifying.Should().Be(1, "no HR from Hevy, so judged on effort: RPE 9 on a hard track counts");
        _walk.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task SameEventTwice_AndARepullWithForce_StillOneTrackSession()
    {
        var events = Fixture("events-upper-a-stair-finisher.json");
        HevyReturns(events[0], events[0]);

        await _service.PullAsync(Joe, force: false);
        await _service.PullAsync(Joe, force: true);

        _stairs.Sessions.Should().ContainSingle();
        _plan.Completions.Should().ContainSingle();
    }

    [Fact]
    public async Task EditedWorkout_RepulledAfterMatching_DoesNotEvaluateTheTrackAgain()
    {
        HevyReturns(Fixture("events-upper-a-stair-finisher.json"));
        await _service.PullAsync(Joe, force: false);

        HevyReturns(Fixture("events-upper-a-stair-finisher-edited.json"));
        await _service.PullAsync(Joe, force: true);

        var session = _stairs.Sessions.Should().ContainSingle().Subject;
        session.Floors.Should().Be(32, "a workout that already completed a session never completes or evaluates again");
    }

    [Fact]
    public async Task WalkingOnlyWorkout_CompletesTheTrackSlot_EvaluatesTheWalkOnce_AndTicksWalks()
    {
        HevyReturns(Fixture("events-walk.json"));

        await _service.PullAsync(Joe, force: false);
        await _service.PullAsync(Joe, force: true);

        var completion = _plan.Completions.Should().ContainSingle().Subject;
        completion.Kind.Should().Be(SessionKind.Cardio);
        completion.CardioTrackId.Should().Be(_walk.Id);
        completion.Cardio!.Source.Should().Be(CardioLogSource.Hevy);
        var session = _walk.Sessions.Should().ContainSingle().Subject;
        session.Key.Should().Be(completion.TrackSessionKey);
        session.DurationSeconds.Should().Be(1200);
        session.DistanceMeters.Should().Be(1900);
        _walk.State.Qualifying.Should().Be(1);
        await _habits.Received(1).AddLogAsync(Arg.Is<HabitLog>(l => l.HabitId == _walksHabit.Id), Arg.Any<CancellationToken>());
    }

    private Exercise AddStairsToUpperA()
    {
        var stairs = Exercise.CreateWithCardioStageProgression("Stair Machine (Floors)", DayNumber.Day1, 2, "4377A52B", CardioLane.Hard);
        _workout.AddExercise(stairs);
        return stairs;
    }

    /// <summary>The recorded stair-finisher workout with the stair machine's RPE replaced.</summary>
    private static HevyWorkoutEvent WithStairRpe(decimal? rpe)
    {
        var recorded = Fixture("events-upper-a-stair-finisher.json")[0];
        var workout = recorded.Workout!;
        return recorded with
        {
            Workout = workout with
            {
                Exercises = workout.Exercises
                    .Select(e => e.ExerciseTemplateId == "4377A52B" ? e with { Sets = e.Sets.Select(set => set with { Rpe = rpe }).ToList() } : e)
                    .ToList()
            }
        };
    }

    [Fact]
    public async Task Pull_CardioWithRpe_CompletesTheLane()
    {
        var stairs = AddStairsToUpperA();
        HevyReturns(WithStairRpe(5m));

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        var performance = _workout.CompletedActivities.Should().ContainSingle().Subject.Performances.Single(p => p.ExerciseId == stairs.Id);
        performance.Cardio.Should().Be(new CardioResult(true, EffortRating.Easy, PainFlag.None, null, totalSeconds: 300, floors: 32));
        stairs.Progression.CalculateCardioBlock()!.Text.Should().Be("3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)", "RPE 5 is Easy, which steps up");
    }

    [Fact]
    public async Task Pull_CardioWithoutRpe_LeavesProgressionUnchanged()
    {
        var stairs = AddStairsToUpperA();
        HevyReturns(WithStairRpe(null));

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        _workout.CompletedActivities.Should().ContainSingle().Subject.Performances.Should().NotContain(p => p.ExerciseId == stairs.Id);
        stairs.Progression.CalculateCardioBlock()!.Text.Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public async Task Pull_StairExerciseOnLiftDay_IsNotLoggedToTheOldTrack()
    {
        AddStairsToUpperA();
        HevyReturns(Fixture("events-upper-a-stair-finisher.json"));

        var result = await _service.PullAsync(Joe, force: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        _workout.CompletedActivities.Should().ContainSingle();
        _stairs.Sessions.Should().BeEmpty("the stair machine belongs to the lift day's cardio exercise now");
    }
}
