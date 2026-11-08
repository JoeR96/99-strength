using A2S.Application.Common;
using A2S.Application.Hevy;
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
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

/// <summary>
/// The idempotent push: link → PUT, no link → POST + link, and review focus 3 (a POST that times out but
/// landed is found by title and linked, so nothing is created twice).
/// </summary>
public class HevyRoutinePusherTests
{
    private const string Key = "test";
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateOnly Wed18Nov = new(2026, 11, 18);
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);

    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly InMemoryLinks _links = new();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<HevyRoutineDraft> _posted = new();
    private readonly List<(string Id, HevyRoutineDraft Draft)> _put = new();
    private readonly HevyRoutinePusher _pusher;

    private readonly Workout _workout;
    private readonly TrainingPlan _plan;
    private readonly CardioProtocol _fourByFour = CardioProtocol.NorwegianFourByFour(Joe);
    private readonly MobilityRoutine _mobilityA;

    public HevyRoutinePusherTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(Now));
        var library = Substitute.For<IExerciseLibraryProvider>();
        library.GetByName(Arg.Any<string>()).Returns((ExerciseTemplate?)null);
        var catalogue = new HevyTemplateCatalogue(_hevy, _links, library, _unitOfWork, clock);
        _pusher = new HevyRoutinePusher(_hevy, _links, catalogue, _workouts, _protocols, _routines, new A2SProgramProvider(), _unitOfWork, clock, NullLogger<HevyRoutinePusher>.Instance);

        _workout = UpperA();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);
        _mobilityA = MobilityRoutine.Create(Joe, CutTo85Nov2026.MobilityAName, 10,
            [new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, null, true)]);
        _routines.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<MobilityRoutine> { _mobilityA });
        _protocols.GetByIdAsync(_fourByFour.Id, Arg.Any<CancellationToken>()).Returns(_fourByFour);

        _plan = TrainingPlan.Create(Joe, CutTo85Nov2026.Name, Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, _fourByFour.Id, "Norwegian 4×4"),
            ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K run or LISS 30–40 min"),
            ScheduledSession.Rest(DayOfWeek.Sunday)
        ], 95m);
        _plan.LinkWorkout(_workout.Id);

        // Hevy: the plan's folder already exists; templates are created on demand; POST/PUT succeed.
        _hevy.GetRoutineFoldersAsync(Key, 1, Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutineFolder>>.Success(new HevyPage<HevyRoutineFolder>
        {
            Page = 1, PageCount = 1, Items = [new HevyRoutineFolder { Id = 42, Title = CutTo85Nov2026.Name }, new HevyRoutineFolder { Id = 7, Title = "Old" }]
        }));
        _hevy.CreateExerciseTemplateAsync(Key, Arg.Any<HevyExerciseTemplateDraft>(), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyExerciseTemplate>.Success(new HevyExerciseTemplate { Id = "T-" + call.Arg<HevyExerciseTemplateDraft>().Title }));
        _hevy.CreateRoutineAsync(Key, Arg.Do<HevyRoutineDraft>(_posted.Add), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyRoutine>.Success(new HevyRoutine { Id = $"R-{_posted.Count}", Title = call.Arg<HevyRoutineDraft>().Title, FolderId = 42 }));
        _hevy.UpdateRoutineAsync(Key, Arg.Any<string>(), Arg.Do<HevyRoutineDraft>(d => _put.Add((string.Empty, d))), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyRoutine>.Success(new HevyRoutine { Id = call.ArgAt<string>(1), Title = call.Arg<HevyRoutineDraft>().Title }));
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

    private ScheduledSession Session(SessionKind kind, DateOnly date) => _plan.SessionsOn(date).First(s => s.Kind == kind);

    [Fact]
    public async Task FirstLiftPush_CreatesTemplatesAndTheRoutine_AndLinksEverything()
    {
        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Outcome.Should().Be(PushRoutineResult.Created);
        result.Value.RoutineId.Should().Be("R-1");
        result.Value.Title.Should().Be("Cut to 85 — Nov 2026 · W1 Upper A");

        var draft = _posted.Should().ContainSingle().Subject;
        draft.FolderId.Should().Be(42);
        draft.Exercises.Select(e => e.ExerciseTemplateId).Should().Equal("T-Open books", "79D0BB3A", "T-Seal Row (Barbell)");
        draft.Exercises[1].Sets[^1].Type.Should().Be(HevySetTypes.Failure);

        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Folder && l.HevyId == "42");
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Template && l.LocalKey == "drill:open-books");
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Template && l.LocalKey == "exercise:seal-row-barbell");
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Routine && l.LocalKey == HevyLocalKeys.LiftRoutine(_plan.Id, Mon16Nov, DayNumber.Day1) && l.HevyId == "R-1");
        await _hevy.DidNotReceive().CreateRoutineFolderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondPush_UpdatesTheLinkedRoutine_AndCreatesNothingNew()
    {
        await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);
        var templateCalls = _hevy.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IHevyClient.CreateExerciseTemplateAsync));

        var again = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        again.Value.Outcome.Should().Be(PushRoutineResult.Updated);
        again.Value.RoutineId.Should().Be("R-1");
        _posted.Should().ContainSingle();
        _put.Should().ContainSingle();
        await _hevy.Received(1).UpdateRoutineAsync(Key, "R-1", Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>());
        _hevy.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IHevyClient.CreateExerciseTemplateAsync)).Should().Be(templateCalls, "templates are remembered");
        _links.All.Count(l => l.Kind == HevyLinkKind.Routine).Should().Be(1);
    }

    [Fact]
    public async Task ReviewFocus3_PostTimesOutButLanded_IsFoundByTitleAndLinked_SoTheRetryUpdates()
    {
        var title = "Cut to 85 — Nov 2026 · W1 Upper A";
        _hevy.CreateRoutineAsync(Key, Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyRoutine>.Failure(HevyError.Unavailable()));
        _hevy.GetRoutinesAsync(Key, 1, Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutine>>.Success(new HevyPage<HevyRoutine>
        {
            Page = 1, PageCount = 2, Items = [new HevyRoutine { Id = "R-other", Title = "Something else" }]
        }));
        _hevy.GetRoutinesAsync(Key, 2, Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutine>>.Success(new HevyPage<HevyRoutine>
        {
            Page = 2, PageCount = 2, Items = [new HevyRoutine { Id = "R-landed", Title = title }]
        }));

        var first = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);
        var retry = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value.RoutineId.Should().Be("R-landed");
        first.Value.Outcome.Should().Be(PushRoutineResult.Created);
        retry.Value.Outcome.Should().Be(PushRoutineResult.Updated);
        retry.Value.RoutineId.Should().Be("R-landed");
        await _hevy.Received(1).CreateRoutineAsync(Key, Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>());
        await _hevy.Received(1).UpdateRoutineAsync(Key, "R-landed", Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>());
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Routine).Which.HevyId.Should().Be("R-landed");
    }

    [Fact]
    public async Task PostTimesOutAndNothingLanded_FailsAsUnavailable_WithNoRoutineLink()
    {
        _hevy.CreateRoutineAsync(Key, Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyRoutine>.Failure(HevyError.Unavailable()));
        _hevy.GetRoutinesAsync(Key, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutine>>.Success(new HevyPage<HevyRoutine> { Page = 1, PageCount = 1 }));

        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ExternalServiceError);
        result.Error.Should().Be("Hevy is unavailable, your change is saved locally");
        _links.All.Should().NotContain(l => l.Kind == HevyLinkKind.Routine);
    }

    [Fact]
    public async Task LinkedRoutineDeletedInHevy_IsRecreated_AndTheLinkMoves()
    {
        var key = HevyLocalKeys.LiftRoutine(_plan.Id, Mon16Nov, DayNumber.Day1);
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Routine, key, "R-gone", Now));
        _hevy.UpdateRoutineAsync(Key, "R-gone", Arg.Any<HevyRoutineDraft>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyRoutine>.Failure(HevyError.NotFound()));

        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        result.Value.Outcome.Should().Be(PushRoutineResult.Created);
        result.Value.RoutineId.Should().Be("R-1");
        _links.All.Single(l => l.Kind == HevyLinkKind.Routine).HevyId.Should().Be("R-1");
    }

    [Fact]
    public async Task InvalidKey_IsSurfacedFromTheFirstHevyCall()
    {
        _hevy.GetRoutineFoldersAsync(Key, 1, Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutineFolder>>.Failure(HevyError.InvalidKey()));

        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Lift, Mon16Nov), Mon16Nov);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Hevy says the key is invalid");
        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task NoFolderYet_CreatesOne_AndRemembersIt()
    {
        _hevy.GetRoutineFoldersAsync(Key, 1, Arg.Any<CancellationToken>()).Returns(HevyResult<HevyPage<HevyRoutineFolder>>.Success(new HevyPage<HevyRoutineFolder> { Page = 1, PageCount = 1 }));
        _hevy.CreateRoutineFolderAsync(Key, CutTo85Nov2026.Name, Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyRoutineFolder>.Success(new HevyRoutineFolder { Id = 99, Title = CutTo85Nov2026.Name }));

        await _pusher.PushAsync(Key, _plan, Session(SessionKind.Cardio, Wed18Nov), Wed18Nov);
        await _pusher.PushAsync(Key, _plan, Session(SessionKind.Cardio, Wed18Nov), Wed18Nov);

        await _hevy.Received(1).CreateRoutineFolderAsync(Key, CutTo85Nov2026.Name, Arg.Any<CancellationToken>());
        await _hevy.Received(1).GetRoutineFoldersAsync(Key, 1, Arg.Any<CancellationToken>());
        _posted.Single().FolderId.Should().Be(99);
        _put.Single().Draft.FolderId.Should().Be(99);
    }

    [Fact]
    public async Task CardioPush_UsesTheSlotKey_AndOneDurationSetPerInterval()
    {
        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Cardio, Wed18Nov), Wed18Nov);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Title.Should().Be("Cut to 85 — Nov 2026 · Norwegian 4×4");
        var draft = _posted.Single();
        draft.Exercises.Single().ExerciseTemplateId.Should().Be("T-Norwegian 4×4");
        draft.Exercises.Single().Sets.Should().HaveCount(_fourByFour.Intervals.Count);
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Routine && l.LocalKey == HevyLocalKeys.SlotRoutine(_plan.Id, SessionKind.Cardio, "Norwegian 4×4"));
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Template && l.LocalKey == "cardio:NorwegianFourByFour");
    }

    [Fact]
    public async Task OpenCardioSlot_CannotBePushed()
    {
        var saturday = new DateOnly(2026, 11, 21);

        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Cardio, saturday), saturday);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task RestDay_CannotBePushed()
    {
        var sunday = new DateOnly(2026, 11, 22);

        var result = await _pusher.PushAsync(Key, _plan, Session(SessionKind.Rest, sunday), sunday);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
    }
}
