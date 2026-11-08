using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Mobility;

public class MobilityRoutineTests
{
    private static readonly UserId Owner = new("user_mobility_0001");

    private static MobilityDrill[] MobilityA() =>
    [
        new MobilityDrill(
            "90/90 breathing", 2, DrillDose.Breaths(5), SideBias.None,
            cues: "Ribs down, long exhale",
            standingAlternative: "Back-to-wall breathing, pelvis tucked, ribs down",
            perSide: false),
        new MobilityDrill(
            "Quadruped hip shift", 2, DrillDose.Breaths(5), SideBias.None,
            cues: null,
            standingAlternative: "Wall-supported hip shift",
            perSide: false),
        new MobilityDrill(
            "Open books", 2, DrillDose.Reps(5), SideBias.Left,
            cues: "Extra set opening the left",
            standingAlternative: "Side-on wall rotation in split stance, hips square",
            perSide: true),
        new MobilityDrill(
            "Band walks + glute bridges", 2, DrillDose.Reps(10), SideBias.None,
            cues: "10 each", standingAlternative: null, perSide: false),
        new MobilityDrill(
            "Wall slides with lift-off", 2, DrillDose.Reps(10), SideBias.None,
            cues: null, standingAlternative: null, perSide: false)
    ];

    [Fact]
    public void Create_WithMobilityADrills_KeepsNamesSetsDosesAndStandingAlternatives()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        routine.UserId.Should().Be(Owner);
        routine.Name.Should().Be("Mobility A");
        routine.TargetMinutes.Should().Be(10);
        routine.Drills.Should().HaveCount(5);
        routine.Drills.Select(d => d.Name).Should().Equal(
            "90/90 breathing", "Quadruped hip shift", "Open books", "Band walks + glute bridges", "Wall slides with lift-off");
        routine.Drills.Should().AllSatisfy(d => d.Sets.Should().Be(2));
        routine.Drills[0].Dose.Should().Be(new DrillDose(DoseUnit.Breaths, 5));
        routine.Drills[0].StandingAlternative.Should().Be("Back-to-wall breathing, pelvis tucked, ribs down");
        routine.Drills[1].StandingAlternative.Should().Be("Wall-supported hip shift");
        routine.Drills[2].Dose.Should().Be(new DrillDose(DoseUnit.Reps, 5));
        routine.Drills[2].PerSide.Should().BeTrue();
        routine.Drills[2].ExtraSetSide.Should().Be(SideBias.Left);
        routine.Drills[2].StandingAlternative.Should().Be("Side-on wall rotation in split stance, hips square");
        routine.Drills[3].Dose.Should().Be(new DrillDose(DoseUnit.Reps, 10));
        routine.Drills[3].StandingAlternative.Should().BeNull();
        routine.Drills[4].Dose.Should().Be(new DrillDose(DoseUnit.Reps, 10));
    }

    [Fact]
    public void Create_WithDuplicateDrillName_Throws()
    {
        var drills = new[]
        {
            new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.None, null, null, true),
            new MobilityDrill("open books", 3, DrillDose.Reps(8), SideBias.None, null, null, true)
        };

        var act = () => MobilityRoutine.Create(Owner, "Dupes", 10, drills);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*unique*");
    }

    [Fact]
    public void Create_WithoutDrills_Throws()
    {
        var act = () => MobilityRoutine.Create(Owner, "Empty", 10, Array.Empty<MobilityDrill>());

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var act = () => MobilityRoutine.Create(Owner, name, 10, MobilityA());

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_WithNonPositiveTargetMinutes_Throws()
    {
        var act = () => MobilityRoutine.Create(Owner, "Mobility A", 0, MobilityA());

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void AddDrill_AppendsToTheEnd()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility B", 25, MobilityA());

        routine.AddDrill(new MobilityDrill("Bird dog", 3, DrillDose.Reps(5), SideBias.Right, null, null, true));

        routine.Drills.Should().HaveCount(6);
        routine.Drills[5].Name.Should().Be("Bird dog");
    }

    [Fact]
    public void AddDrill_WithExistingName_Throws()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        var act = () => routine.AddDrill(new MobilityDrill("Open Books", 1, DrillDose.Reps(5), SideBias.None, null, null, false));

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void RemoveDrill_KeepsTheOrderOfTheRest()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        var removed = routine.RemoveDrill("Open books");

        removed.Should().BeTrue();
        routine.Drills.Select(d => d.Name).Should().Equal(
            "90/90 breathing", "Quadruped hip shift", "Band walks + glute bridges", "Wall slides with lift-off");
    }

    [Fact]
    public void RemoveDrill_UnknownName_ReturnsFalse()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        routine.RemoveDrill("Nope").Should().BeFalse();
        routine.Drills.Should().HaveCount(5);
    }

    [Fact]
    public void RemoveDrill_LastDrill_Throws()
    {
        var routine = MobilityRoutine.Create(Owner, "Solo", 5,
            [new MobilityDrill("Only", 1, DrillDose.Seconds(30), SideBias.None, null, null, false)]);

        var act = () => routine.RemoveDrill("Only");

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void MoveDrill_ToTheFront_ReordersWithoutLosingAny()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        routine.MoveDrill("Wall slides with lift-off", 0);

        routine.Drills.Select(d => d.Name).Should().Equal(
            "Wall slides with lift-off", "90/90 breathing", "Quadruped hip shift", "Open books", "Band walks + glute bridges");
    }

    [Fact]
    public void MoveDrill_ToTheEnd_ReordersWithoutLosingAny()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        routine.MoveDrill("90/90 breathing", 4);

        routine.Drills.Select(d => d.Name).Should().Equal(
            "Quadruped hip shift", "Open books", "Band walks + glute bridges", "Wall slides with lift-off", "90/90 breathing");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void MoveDrill_OutOfRange_Throws(int index)
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        var act = () => routine.MoveDrill("Open books", index);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void MoveDrill_UnknownName_Throws()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        var act = () => routine.MoveDrill("Nope", 0);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ReplaceDrills_SwapsTheWholeList()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());
        var replacement = new[]
        {
            new MobilityDrill("Bird dog", 3, DrillDose.Reps(5), SideBias.Right, null, null, true),
            new MobilityDrill("Side plank", 3, DrillDose.Seconds(30), SideBias.Left, null, null, true)
        };

        routine.ReplaceDrills(replacement);

        routine.Drills.Should().Equal(replacement);
    }

    [Fact]
    public void ReplaceDrills_WithEmptyList_Throws()
    {
        var routine = MobilityRoutine.Create(Owner, "Mobility A", 10, MobilityA());

        var act = () => routine.ReplaceDrills(Array.Empty<MobilityDrill>());

        act.Should().Throw<BusinessRuleViolationException>();
        routine.Drills.Should().HaveCount(5);
    }

    [Fact]
    public void MobilityDrill_RejectsBlankNameAndNonPositiveSets()
    {
        var blank = () => new MobilityDrill(" ", 2, DrillDose.Reps(5), SideBias.None, null, null, false);
        var zeroSets = () => new MobilityDrill("Open books", 0, DrillDose.Reps(5), SideBias.None, null, null, false);

        blank.Should().Throw<BusinessRuleViolationException>();
        zeroSets.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void DrillDose_RejectsNonPositiveAmount()
    {
        var act = () => new DrillDose(DoseUnit.Seconds, 0);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
