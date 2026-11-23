using A2S.Domain.Aggregates.Sync;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Sync;

public class ChangeStampTests
{
    [Theory]
    [InlineData(SyncArea.Habits, "habits")]
    [InlineData(SyncArea.Plan, "plan")]
    [InlineData(SyncArea.Workout, "workout")]
    [InlineData(SyncArea.Cardio, "cardio")]
    [InlineData(SyncArea.Mobility, "mobility")]
    public void Bump_RaisesTheVersion_AndOnlyTheTouchedArea(SyncArea area, string name)
    {
        var before = ChangeStamp.Empty.Bump(SyncArea.All);

        var after = before.Bump(area);

        after.Version.Should().Be(2);
        after.Of(area).Should().Be(2);
        after.Areas().Where(a => a.Key != name).Should().AllSatisfy(a => a.Value.Should().Be(1));
        after.ChangedSince(1).Should().Be(area);
    }

    [Fact]
    public void Bump_SeveralAreas_TakeTheSameVersion()
    {
        var stamp = ChangeStamp.Empty.Bump(SyncArea.Habits).Bump(SyncArea.Plan | SyncArea.Cardio);

        stamp.Should().Be(new ChangeStamp(2, 1, 2, 0, 2, 0));
        SyncAreas.Names(stamp.ChangedSince(1)).Should().Equal("plan", "cardio");
        stamp.ChangedSince(2).Should().Be(SyncArea.None);
        stamp.ChangedSince(0).Should().Be(SyncArea.Habits | SyncArea.Plan | SyncArea.Cardio);
    }

    [Fact]
    public void Areas_ListsEveryAreaInWireOrder()
    {
        ChangeStamp.Empty.Areas().Keys.Should().Equal("habits", "plan", "workout", "cardio", "mobility");
    }
}
