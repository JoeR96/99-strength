using System.Text.Json;
using A2S.Application.Commands.Hevy;
using A2S.Application.Commands.Plans;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Queries.Hevy;
using A2S.Application.Services;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

/// <summary>Working the inbox: assign completes (as an extra session here) and leaves the inbox; ignore dismisses.</summary>
public class AssignHevyWorkoutCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Sun22Nov = new(2026, 11, 22);
    private static readonly DateOnly Sat21Nov = new(2026, 11, 21);

    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));
    private readonly InMemorySnapshots _snapshots = new();
    private readonly InMemoryLinks _links = new();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly TrainingPlan _plan;
    private readonly AssignHevyWorkoutCommandHandler _assign;
    private readonly IgnoreHevyWorkoutCommandHandler _ignore;
    private readonly GetHevyInboxQueryHandler _inbox;

    public AssignHevyWorkoutCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _plan = TrainingPlan.Create(Joe, "Cut", new DateOnly(2026, 11, 16),
            [ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K run or LISS"), ScheduledSession.Rest(DayOfWeek.Sunday)], 95m);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(_plan);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);

        var completeSession = new CompleteSessionCommandHandler(_plans, _unitOfWork, _currentUser, _clock, _protocols);
        _sender.Send(Arg.Any<CompleteSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => completeSession.Handle(call.Arg<CompleteSessionCommand>(), CancellationToken.None));

        var catalogue = new HevyTemplateCatalogue(Substitute.For<IHevyClient>(), _links, Substitute.For<IExerciseLibraryProvider>(), _unitOfWork, _clock);
        var completer = new HevySessionCompleter(_sender, _routines);
        _assign = new AssignHevyWorkoutCommandHandler(_snapshots, _plans, _workouts, catalogue, completer, _currentUser, _unitOfWork, _clock);
        _ignore = new IgnoreHevyWorkoutCommandHandler(_snapshots, _currentUser, _unitOfWork, _clock);
        _inbox = new GetHevyInboxQueryHandler(_snapshots, _currentUser);
    }

    private HevyWorkoutSnapshot UnmatchedRun(string id = "w-run", UserId? owner = null)
    {
        var workout = new HevyWorkout
        {
            Id = id,
            Title = "Sunday jog",
            StartTime = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 11, 22, 9, 35, 0, TimeSpan.Zero),
            Exercises = [new HevyWorkoutExercise { Title = "Running", ExerciseTemplateId = "AC1BF3C1", Sets = [new HevyWorkoutSet { DurationSeconds = 2100, DistanceMeters = 5000 }] }]
        };
        var snapshot = HevyWorkoutSnapshot.Create(new HevyWorkoutId(id), owner ?? Joe, workout.Title, workout.StartTime, workout.EndTime, null, JsonSerializer.Serialize(workout), Sun22Nov, Now);
        snapshot.MarkUnmatched("rest day", Now);
        _snapshots.AddAsync(snapshot).GetAwaiter().GetResult();
        return snapshot;
    }

    [Fact]
    public async Task Inbox_ListsUnmatchedSnapshots_WithTheirExercises()
    {
        UnmatchedRun();

        var inbox = await _inbox.Handle(new GetHevyInboxQuery(), CancellationToken.None);

        var item = inbox.Value.Should().ContainSingle().Subject;
        item.HevyWorkoutId.Should().Be("w-run");
        item.Reason.Should().Be("rest day");
        item.LocalDate.Should().Be(Sun22Nov);
        item.Exercises.Should().ContainSingle().Which.Should().Be(new HevyInboxExerciseDto("Running", "AC1BF3C1", 1));
    }

    [Fact]
    public async Task Assign_ToSaturdaysCardioSlot_CompletesIt_AndRemovesItFromTheInbox()
    {
        var snapshot = UnmatchedRun();

        var result = await _assign.Handle(new AssignHevyWorkoutCommand("w-run", Sat21Nov, SessionKind.Cardio), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Date.Should().Be(Sat21Nov);
        var completion = _plan.Completions.Should().ContainSingle().Subject;
        completion.Id.Value.Should().Be(result.Value.CompletionId);
        completion.Kind.Should().Be(SessionKind.Cardio);
        completion.Source.Should().Be(CompletionSource.Hevy);
        completion.HevyWorkoutId.Should().Be("w-run");
        completion.Cardio!.DurationSeconds.Should().Be(2100);
        completion.Cardio.DistanceMeters.Should().Be(5000m);
        snapshot.Status.Should().Be(SnapshotStatus.Matched);
        snapshot.SessionCompletionId.Should().Be(completion.Id);
        (await _inbox.Handle(new GetHevyInboxQuery(), CancellationToken.None)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Assign_Twice_IsRefusedAsAConflict()
    {
        UnmatchedRun();
        await _assign.Handle(new AssignHevyWorkoutCommand("w-run", Sat21Nov, SessionKind.Cardio), CancellationToken.None);

        var again = await _assign.Handle(new AssignHevyWorkoutCommand("w-run", Sat21Nov, SessionKind.Cardio), CancellationToken.None);

        again.ErrorCode.Should().Be(ErrorCode.Conflict);
        _plan.Completions.Should().ContainSingle();
    }

    [Fact]
    public async Task Assign_SomeoneElsesWorkout_IsNotFound()
    {
        UnmatchedRun("w-theirs", Other);

        var result = await _assign.Handle(new AssignHevyWorkoutCommand("w-theirs", Sat21Nov, SessionKind.Cardio), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        _plan.Completions.Should().BeEmpty();
    }

    [Fact]
    public async Task Ignore_DismissesIt_AndIgnoringAMatchedOneIsRefused()
    {
        var snapshot = UnmatchedRun();

        var ignored = await _ignore.Handle(new IgnoreHevyWorkoutCommand("w-run"), CancellationToken.None);
        ignored.IsSuccess.Should().BeTrue();
        snapshot.Status.Should().Be(SnapshotStatus.Ignored);
        (await _inbox.Handle(new GetHevyInboxQuery(), CancellationToken.None)).Value.Should().BeEmpty();

        var matched = UnmatchedRun("w-done");
        matched.MarkMatched(new SessionCompletionId(Guid.NewGuid()), Now);
        var refused = await _ignore.Handle(new IgnoreHevyWorkoutCommand("w-done"), CancellationToken.None);
        refused.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);

        var missing = await _ignore.Handle(new IgnoreHevyWorkoutCommand("nope"), CancellationToken.None);
        missing.ErrorCode.Should().Be(ErrorCode.NotFound);
    }
}
