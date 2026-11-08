using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.HevySync;

public class HevyWorkoutSnapshotTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly HevyWorkoutId Id = new("b3c2a1d0-1111-4222-8333-444455556666");
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 17, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 11, 16, 18, 40, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 11, 16);
    private static readonly SessionCompletionId Completion = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));

    private static HevyWorkoutSnapshot Snapshot() =>
        HevyWorkoutSnapshot.Create(Id, Joe, " Upper A ", Start, End, " r-1 ", "{}", Date, End.AddMinutes(1));

    [Fact]
    public void Create_StartsPending_AndAwaitsMatching()
    {
        var snapshot = Snapshot();

        snapshot.Status.Should().Be(SnapshotStatus.Pending);
        snapshot.AwaitsMatching.Should().BeTrue();
        snapshot.Title.Should().Be("Upper A");
        snapshot.RoutineId.Should().Be("r-1");
        snapshot.LocalDate.Should().Be(Date);
        snapshot.SessionCompletionId.Should().BeNull();
    }

    [Fact]
    public void Create_RejectsAnEndBeforeTheStart_AndEmptyJson()
    {
        var backwards = () => HevyWorkoutSnapshot.Create(Id, Joe, "x", End, Start, null, "{}", Date, End);
        var noJson = () => HevyWorkoutSnapshot.Create(Id, Joe, "x", Start, End, null, " ", Date, End);

        backwards.Should().Throw<BusinessRuleViolationException>();
        noJson.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void MarkMatched_ThenRefresh_StaysMatched()
    {
        var snapshot = Snapshot();
        snapshot.MarkMatched(Completion, End.AddMinutes(2));

        snapshot.Refresh("Upper A (edited)", Start, End.AddMinutes(5), "r-1", "{\"edited\":true}", Date, End.AddMinutes(10));

        snapshot.Status.Should().Be(SnapshotStatus.Matched);
        snapshot.SessionCompletionId.Should().Be(Completion);
        snapshot.AwaitsMatching.Should().BeFalse();
        snapshot.Title.Should().Be("Upper A (edited)");
        snapshot.RawJson.Should().Be("{\"edited\":true}");
        snapshot.UpdatedAtUtc.Should().Be(End.AddMinutes(10));
    }

    [Fact]
    public void MatchedSnapshot_CannotBeUnmatchedOrIgnored()
    {
        var snapshot = Snapshot();
        snapshot.MarkMatched(Completion, End);

        var unmatched = () => snapshot.MarkUnmatched("nope", End);
        var ignored = () => snapshot.Ignore(End);

        unmatched.Should().Throw<BusinessRuleViolationException>();
        ignored.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Deleted_KeepsTheCompletion_AndARestoreGoesBackToPending()
    {
        var matched = Snapshot();
        matched.MarkMatched(Completion, End);
        matched.MarkDeletedInHevy(End.AddDays(1));

        matched.Status.Should().Be(SnapshotStatus.DeletedInHevy);
        matched.SessionCompletionId.Should().Be(Completion);
        matched.AwaitsMatching.Should().BeFalse();

        matched.Refresh("Upper A", Start, End, "r-1", "{}", Date, End.AddDays(2));
        matched.Status.Should().Be(SnapshotStatus.Pending);
        matched.Reason.Should().BeNull();
    }

    [Fact]
    public void Unmatched_CarriesAReason_AndIgnoreClearsIt()
    {
        var snapshot = Snapshot();

        snapshot.MarkUnmatched("rest day", End);
        snapshot.Status.Should().Be(SnapshotStatus.Unmatched);
        snapshot.Reason.Should().Be("rest day");
        snapshot.AwaitsMatching.Should().BeTrue();

        snapshot.Ignore(End.AddMinutes(1));
        snapshot.Status.Should().Be(SnapshotStatus.Ignored);
        snapshot.Reason.Should().BeNull();
        snapshot.AwaitsMatching.Should().BeFalse();
    }
}
