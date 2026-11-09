using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.HevySync;

public class HevyLinkTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly TrainingPlanId Plan = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    [Fact]
    public void Create_TrimsAndStampsUtc()
    {
        var link = HevyLink.Create(Joe, HevyLinkKind.Routine, " plan:x ", " abc123 ", new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1)));

        link.LocalKey.Should().Be("plan:x");
        link.HevyId.Should().Be("abc123");
        link.SyncedAtUtc.Should().Be(At);
        link.Kind.Should().Be(HevyLinkKind.Routine);
        link.UserId.Should().Be(Joe);
    }

    [Fact]
    public void Create_RejectsBlankKeys()
    {
        var noKey = () => HevyLink.Create(Joe, HevyLinkKind.Template, " ", "abc", At);
        var noId = () => HevyLink.Create(Joe, HevyLinkKind.Template, "drill:x", "", At);

        noKey.Should().Throw<BusinessRuleViolationException>();
        noId.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Relink_ChangesTheHevyId_AndTouchKeepsIt()
    {
        var link = HevyLink.Create(Joe, HevyLinkKind.Routine, "plan:x", "old", At);

        link.Relink("new", At.AddHours(1));
        link.HevyId.Should().Be("new");
        link.SyncedAtUtc.Should().Be(At.AddHours(1));

        link.Touch(At.AddHours(2));
        link.HevyId.Should().Be("new");
        link.SyncedAtUtc.Should().Be(At.AddHours(2));
    }

    [Fact]
    public void LocalKeys_AreStable_AndRoundTripThroughTryParse()
    {
        var date = new DateOnly(2026, 11, 16);
        var liftKey = HevyLocalKeys.LiftRoutine(Plan, date, DayNumber.Day1);
        var slotKey = HevyLocalKeys.SlotRoutine(Plan, SessionKind.Cardio, "Norwegian 4×4 — outdoors, hills");

        liftKey.Should().Be("plan:11111111-2222-3333-4444-555555555555:20261116:Lift:Day1");
        slotKey.Should().Be("plan:11111111-2222-3333-4444-555555555555:Cardio:norwegian-4×4-—-outdoors,-hills");
        HevyLocalKeys.Folder(Plan).Should().Be("plan:11111111-2222-3333-4444-555555555555:folder");
        HevyLocalKeys.DrillTemplate("90/90 breathing").Should().Be("drill:90/90-breathing");
        HevyLocalKeys.CardioTemplate(Domain.Aggregates.Cardio.CardioProtocolKind.Liss).Should().Be("cardio:Liss");

        HevyLocalKeys.TryParseRoutine(liftKey, out var lift).Should().BeTrue();
        lift.PlanId.Should().Be(Plan);
        lift.Kind.Should().Be(SessionKind.Lift);
        lift.Date.Should().Be(date);
        lift.LiftDay.Should().Be(DayNumber.Day1);
        lift.Describes(ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")).Should().BeTrue();
        lift.Describes(ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A")).Should().BeFalse();

        HevyLocalKeys.TryParseRoutine(slotKey, out var slot).Should().BeTrue();
        slot.Kind.Should().Be(SessionKind.Cardio);
        slot.Describes(ScheduledSession.Cardio(DayOfWeek.Wednesday, null, "Norwegian 4×4 — outdoors, hills")).Should().BeTrue();
        slot.Describes(ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K run or LISS")).Should().BeFalse();

        HevyLocalKeys.TryParseRoutine("drill:open-books", out _).Should().BeFalse();
        HevyLocalKeys.TryParseRoutine(HevyLocalKeys.Folder(Plan), out _).Should().BeFalse();
    }

    [Fact]
    public void RoutineKey_PicksLiftOrSlotFormByKind()
    {
        var date = new DateOnly(2026, 11, 18);

        HevyLocalKeys.Routine(Plan, date, ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day3, "Upper B"))
            .Should().EndWith(":20261118:Lift:Day3");
        HevyLocalKeys.Routine(Plan, date, ScheduledSession.Mobility(DayOfWeek.Wednesday, null, "Mobility B (25 min)"))
            .Should().EndWith(":Mobility:mobility-b-(25-min)");
    }
}
