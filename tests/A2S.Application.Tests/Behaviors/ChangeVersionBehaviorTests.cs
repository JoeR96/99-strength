using A2S.Application.Behaviors;
using A2S.Application.Commands.CardioTracks;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Commands.Habits;
using A2S.Application.Commands.Mobility;
using A2S.Application.Commands.Plans;
using A2S.Application.Commands.SetTrainingMax;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Commands.UpdateExercises;
using A2S.Application.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace A2S.Application.Tests.Behaviors;

[ChangesSyncArea(SyncArea.Habits)]
public sealed record FakeHabitWrite : ICommand<Result>;

public sealed record FakeUnmarkedWrite : ICommand<Result>;

[ChangesSyncArea(SyncArea.Cardio | SyncArea.Plan)]
public sealed record FakePlainWrite : ICommand<int>;

public class ChangeVersionBehaviorTests
{
    private const string User = "user_sync_01";

    private readonly IUserChangeLog _log = Substitute.For<IUserChangeLog>();
    private readonly ISyncNotifier _notifier = Substitute.For<ISyncNotifier>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    public ChangeVersionBehaviorTests()
    {
        _currentUser.UserId.Returns(User);
        _log.BumpAsync(User, Arg.Any<SyncArea>(), Arg.Any<CancellationToken>()).Returns(new ChangeStamp(124, 124, 0, 0, 0, 0));
    }

    private ChangeVersionBehavior<TRequest, TResponse> Behavior<TRequest, TResponse>() where TRequest : notnull =>
        new(_log, _notifier, _currentUser, NullLogger<ChangeVersionBehavior<TRequest, TResponse>>.Instance);

    [Fact]
    public async Task SuccessfulMarkedCommand_BumpsItsAreas_AndPublishesTheNewVersion()
    {
        var result = await Behavior<FakeHabitWrite, Result>().Handle(new FakeHabitWrite(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _log.Received(1).BumpAsync(User, SyncArea.Habits, Arg.Any<CancellationToken>());
        _notifier.Received(1).Publish(User, new SyncChange(124, SyncArea.Habits));
    }

    [Fact]
    public async Task FailedCommand_DoesNotBump()
    {
        await Behavior<FakeHabitWrite, Result>().Handle(new FakeHabitWrite(), _ => Task.FromResult(Result.Failure("nope")), CancellationToken.None);

        await _log.DidNotReceiveWithAnyArgs().BumpAsync(default!, default, default);
        _notifier.DidNotReceiveWithAnyArgs().Publish(default!, default!);
    }

    [Fact]
    public async Task UnmarkedCommand_DoesNotBump()
    {
        await Behavior<FakeUnmarkedWrite, Result>().Handle(new FakeUnmarkedWrite(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        await _log.DidNotReceiveWithAnyArgs().BumpAsync(default!, default, default);
    }

    [Fact]
    public async Task NonResultResponse_CountsAsSuccess()
    {
        await Behavior<FakePlainWrite, int>().Handle(new FakePlainWrite(), _ => Task.FromResult(3), CancellationToken.None);

        await _log.Received(1).BumpAsync(User, SyncArea.Cardio | SyncArea.Plan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABumpThatFails_NeverFailsTheWrite()
    {
        _log.BumpAsync(User, Arg.Any<SyncArea>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var result = await Behavior<FakeHabitWrite, Result>().Handle(new FakeHabitWrite(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifier.DidNotReceiveWithAnyArgs().Publish(default!, default!);
    }

    [Fact]
    public async Task NoUser_DoesNotBump()
    {
        _currentUser.UserId.Returns((string?)null);

        await Behavior<FakeHabitWrite, Result>().Handle(new FakeHabitWrite(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        await _log.DidNotReceiveWithAnyArgs().BumpAsync(default!, default, default);
    }

    /// <summary>The contract's writes, one or more per area, each declaring the area clients refetch.</summary>
    public static TheoryData<Type, SyncArea> ContractWrites => new()
    {
        { typeof(CreateHabitCommand), SyncArea.Habits },
        { typeof(UpdateHabitCommand), SyncArea.Habits },
        { typeof(ArchiveHabitCommand), SyncArea.Habits },
        { typeof(UnarchiveHabitCommand), SyncArea.Habits },
        { typeof(UpsertHabitLogCommand), SyncArea.Habits },
        { typeof(IncrementHabitCommand), SyncArea.Habits },
        { typeof(ImportHabitsCommand), SyncArea.Habits },
        { typeof(UpdateScheduleCommand), SyncArea.Plan },
        { typeof(CompleteSessionCommand), SyncArea.Plan },
        { typeof(UndoSessionCompletionCommand), SyncArea.Plan },
        { typeof(ApplyPlanTemplateCommand), SyncArea.Plan },
        { typeof(CompleteDayCommand), SyncArea.Workout },
        { typeof(SetTrainingMaxCommand), SyncArea.Workout },
        { typeof(UpdateExercisesCommand), SyncArea.Workout },
        { typeof(FinishGymSessionCommand), SyncArea.Workout },
        { typeof(LogCardioTrackSessionCommand), SyncArea.Cardio },
        { typeof(DecideCardioTrackSuggestionCommand), SyncArea.Cardio },
        { typeof(UpdateCardioTrackCommand), SyncArea.Cardio },
        { typeof(FinishGymSessionCommand), SyncArea.Cardio },
        { typeof(UpdateMobilityRoutineCommand), SyncArea.Mobility },
        { typeof(ReplaceDrillsCommand), SyncArea.Mobility },
        { typeof(RecordDrillFeedbackCommand), SyncArea.Mobility },
    };

    [Theory]
    [MemberData(nameof(ContractWrites))]
    public void ContractWrite_DeclaresItsArea(Type command, SyncArea area)
    {
        var declared = command.GetCustomAttributes(typeof(ChangesSyncAreaAttribute), false).OfType<ChangesSyncAreaAttribute>().SingleOrDefault();

        declared.Should().NotBeNull($"{command.Name} writes {area}");
        declared!.Areas.HasFlag(area).Should().BeTrue();
    }

    [Fact]
    public void EveryCommandOfTheSyncedFeatures_IsMarked()
    {
        var synced = new[] { "Habits", "Plans", "CardioTracks", "Cardio", "Mobility", "GymSessions", "SuggestedPlans", "CompleteDay",
            "SetTrainingMax", "UpdateExercises", "SubstituteExercise", "UndoCompletion", "ProgressWeek", "RemoveExercise" };
        var commands = typeof(ICommand<>).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal) && !t.IsAbstract
                && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                && synced.Any(s => t.Namespace == $"A2S.Application.Commands.{s}"))
            .ToList();

        commands.Should().NotBeEmpty();
        commands.Where(t => t.GetCustomAttributes(typeof(ChangesSyncAreaAttribute), false).Length == 0)
            .Select(t => t.Name).Should().BeEmpty("every write in a synced feature bumps the change stamp");
    }
}
