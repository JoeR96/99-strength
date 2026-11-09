using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Events;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.BodyWeight;

public class BodyWeightEntryEventsTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset At = new(2026, 11, 16, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_RaisesBodyWeightRecorded()
    {
        var entry = BodyWeightEntry.Create(Joe, Mon16Nov, 94.3m, At);

        var evt = entry.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BodyWeightRecorded>().Subject;
        evt.UserId.Should().Be(Joe);
        evt.Date.Should().Be(Mon16Nov);
        evt.Kg.Should().Be(94.3m);
        evt.OccurredOn.Should().Be(At.UtcDateTime);
    }

    [Fact]
    public void Update_RaisesItAgainWithTheCorrectedWeight()
    {
        var entry = BodyWeightEntry.Create(Joe, Mon16Nov, 94.3m, At);
        entry.ClearDomainEvents();

        entry.Update(94.0m, At.AddHours(1));

        entry.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BodyWeightRecorded>().Which.Kg.Should().Be(94.0m);
    }
}
