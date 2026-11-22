using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Readiness;

public class ReadinessEntryTests
{
    private static readonly UserId Owner = new("user_ready_0001");
    private static readonly DateOnly Day = new(2026, 11, 22);
    private static readonly DateTimeOffset At = new(2026, 11, 22, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Red_SkipsTheFinisherByDefault_AndSuggestsSkippingHardCardio()
    {
        var entry = ReadinessEntry.Create(Owner, Day, ReadinessColour.Red, At);

        entry.FinisherSkipped.Should().BeTrue();
        var advice = ReadinessAdvice.For(entry);
        advice.SkipFinisher.Should().BeTrue();
        advice.SkipHardCardio.Should().BeTrue();
        advice.Suggestions.Should().Contain("Walks and home routines are fine.");
    }

    [Fact]
    public void Red_KeepFinisher_UndoesTheSkip()
    {
        var entry = ReadinessEntry.Create(Owner, Day, ReadinessColour.Red, At);

        entry.SetKeepFinisher(true, At.AddMinutes(1));

        entry.FinisherSkipped.Should().BeFalse();
        ReadinessAdvice.For(entry).SkipFinisher.Should().BeFalse();
        ReadinessAdvice.For(entry).SkipHardCardio.Should().BeTrue();
    }

    [Fact]
    public void Yellow_IsAGentleNoteOnly()
    {
        var advice = ReadinessAdvice.For(ReadinessEntry.Create(Owner, Day, ReadinessColour.Yellow, At));

        advice.SkipFinisher.Should().BeFalse();
        advice.SkipHardCardio.Should().BeFalse();
        advice.Suggestions.Should().ContainSingle().Which.Should().Contain("Nothing in your plan changes");
    }

    [Fact]
    public void GreenOrNothing_HasNoAdvice()
    {
        ReadinessAdvice.For(ReadinessEntry.Create(Owner, Day, ReadinessColour.Green, At)).Headline.Should().BeNull();
        ReadinessAdvice.For(null).Suggestions.Should().BeEmpty();
    }

    [Fact]
    public void ChangingColour_ResetsTheFinisherChoice()
    {
        var entry = ReadinessEntry.Create(Owner, Day, ReadinessColour.Red, At);
        entry.SetKeepFinisher(true, At);

        entry.SetColour(ReadinessColour.Yellow, At);
        entry.SetColour(ReadinessColour.Red, At);

        entry.FinisherSkipped.Should().BeTrue();
    }

    [Fact]
    public void KeepFinisher_OnANonRedDay_Throws()
    {
        var entry = ReadinessEntry.Create(Owner, Day, ReadinessColour.Green, At);

        var act = () => entry.SetKeepFinisher(true, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
