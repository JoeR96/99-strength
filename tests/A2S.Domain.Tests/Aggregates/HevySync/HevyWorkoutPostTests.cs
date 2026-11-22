using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.HevySync;

public class HevyWorkoutPostTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly Guid ClientSession = Guid.Parse("9a9a9a9a-0000-0000-0000-000000000001");
    private static readonly TrainingPlanId Plan = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    private static readonly SessionCompletionId Completion = new(Guid.Parse("22222222-2222-3333-4444-555555555555"));
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 11, 16, 19, 5, 0, TimeSpan.Zero);

    private static HevyWorkoutPost Create() =>
        HevyWorkoutPost.Create(Joe, ClientSession, Plan, Completion, new DateOnly(2026, 11, 16), " Upper A ", Start, End, "{\"x\":1}", "{\"r\":1}", End);

    [Fact]
    public void Create_IsPendingAndDueImmediately()
    {
        var post = Create();

        post.Status.Should().Be(HevyPostStatus.Pending);
        post.Title.Should().Be("Upper A");
        post.Attempts.Should().Be(0);
        post.IsDue(End).Should().BeTrue();
        post.MayAlreadyExistInHevy.Should().BeFalse();
        post.ClientSessionId.Should().Be(ClientSession);
    }

    [Fact]
    public void Create_RejectsAnEmptyClientSessionIdOrBackwardsTimes()
    {
        var noId = () => HevyWorkoutPost.Create(Joe, Guid.Empty, Plan, Completion, new DateOnly(2026, 11, 16), "Upper A", Start, End, "{}", "{}", End);
        var backwards = () => HevyWorkoutPost.Create(Joe, ClientSession, Plan, Completion, new DateOnly(2026, 11, 16), "Upper A", End, Start, "{}", "{}", End);

        noId.Should().Throw<BusinessRuleViolationException>();
        backwards.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void RecordFailure_BacksOffExponentially()
    {
        var post = Create();

        post.RecordFailure("Hevy unavailable", permanent: false, End);
        post.NextAttemptAtUtc.Should().Be(End.AddMinutes(2));
        post.IsDue(End.AddMinutes(1)).Should().BeFalse();

        post.RecordFailure("Hevy unavailable", permanent: false, End);
        post.NextAttemptAtUtc.Should().Be(End.AddMinutes(4));
        post.Attempts.Should().Be(2);
        post.MayAlreadyExistInHevy.Should().BeTrue();
        post.Status.Should().Be(HevyPostStatus.Pending);
    }

    [Fact]
    public void RecordFailure_PermanentOrTooMany_StopsRetrying()
    {
        var permanent = Create();
        permanent.RecordFailure("bad key", permanent: true, End);

        var many = Create();
        for (var i = 0; i < HevyWorkoutPost.MaxAttempts; i++)
        {
            many.RecordFailure("down", permanent: false, End);
        }

        permanent.Status.Should().Be(HevyPostStatus.Failed);
        many.Status.Should().Be(HevyPostStatus.Failed);
        many.IsDue(End.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void RequeueNow_MakesAFailedPostDueAgain()
    {
        var post = Create();
        post.RecordFailure("bad key", permanent: true, End);

        post.RequeueNow(End.AddHours(1));

        post.Status.Should().Be(HevyPostStatus.Pending);
        post.IsDue(End.AddHours(1)).Should().BeTrue();
    }

    [Fact]
    public void MarkPosted_IsIdempotentForTheSameId_AndRefusesASecondWorkout()
    {
        var post = Create();
        post.RecordFailure("down", permanent: false, End);

        post.MarkPosted("hevy-1", End.AddMinutes(3));
        post.MarkPosted("hevy-1", End.AddMinutes(4));
        var second = () => post.MarkPosted("hevy-2", End.AddMinutes(5));

        post.Status.Should().Be(HevyPostStatus.Posted);
        post.HevyWorkoutId.Should().Be("hevy-1");
        post.PostedAtUtc.Should().Be(End.AddMinutes(3));
        post.LastError.Should().BeNull();
        post.IsDue(End.AddDays(1)).Should().BeFalse();
        second.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void MarkSkipped_IsNotDue_UntilRequeued_AndRecordResultStoresTheAnswer()
    {
        var post = Create();

        post.MarkSkipped("no Hevy key");
        var dueWhileSkipped = post.IsDue(End);
        post.RecordResult("{\"done\":true}");
        post.RequeueNow(End);

        dueWhileSkipped.Should().BeFalse();
        post.Status.Should().Be(HevyPostStatus.Pending);
        post.ResultJson.Should().Be("{\"done\":true}");
    }

    [Fact]
    public void Describes_MatchesTitleAndStartWithinAMinute()
    {
        var post = Create();

        post.Describes("Upper A", Start.AddSeconds(30)).Should().BeTrue();
        post.Describes("Upper A", Start.AddMinutes(5)).Should().BeFalse();
        post.Describes("Lower A", Start).Should().BeFalse();
    }
}
