using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.GymPlanFit;

/// <summary>Plan v3's routines use real kinds and drill levels (Easier / Standard / Harder, the reverse plank ladder).</summary>
public class PlanV3MobilityLevelsTests
{
    private static readonly DateOnly Mon23Nov2026 = new(2026, 11, 23);

    private static SuggestedPlanSpec Spec() => new PlanV3BaseBuildTransition().Build(new PlanTemplateInputs(
        new Dictionary<string, decimal?>(), 95m, Mon23Nov2026));

    private static MobilityRoutineSpec Routine(string name) => Spec().Routines.Single(r => r.Name == name);

    private static MobilityDrillDto Drill(string routine, string drill) => Routine(routine).Drills.Single(d => d.Name == drill);

    private static readonly string[] HomeRoutines = ["Hips & lower back", "Upper back & chest", "Full-body flow"];

    [Fact]
    public void Routines_have_kinds()
    {
        Spec().Routines.Select(r => (r.Name, r.Kind)).Should().Equal(
            ("Warm-up (upper)", MobilityRoutineKind.WarmUp),
            ("Warm-up (lower)", MobilityRoutineKind.WarmUp),
            ("Hips & lower back", MobilityRoutineKind.Corrective),
            ("Upper back & chest", MobilityRoutineKind.Corrective),
            ("Full-body flow", MobilityRoutineKind.YogaFlow));
    }

    [Fact]
    public void Every_home_pose_has_easier_standard_harder_levels_starting_at_standard_except_the_ladder()
    {
        var drills = HomeRoutines.SelectMany(r => Routine(r).Drills).Where(d => d.Name != "Reverse plank ladder").ToList();

        drills.Should().NotBeEmpty();
        drills.Should().OnlyContain(d => d.Levels!.Select(l => l.Name).SequenceEqual(new[] { "Easier", "Standard", "Harder" }));
        drills.Should().OnlyContain(d => d.CurrentLevel == 1);
        drills.Should().OnlyContain(d => d.Cues == null || !d.Cues.Contains("Easier:"));
        drills.Should().OnlyContain(d => d.Levels!.All(l => l.Sets == d.Sets && l.Dose.Unit == d.Dose.Unit));
    }

    [Fact]
    public void Levels_carry_the_easier_and_harder_versions()
    {
        var lunge = Drill("Full-body flow", "Low lunge with overhead reach");

        lunge.Levels![0].Description.Should().Be("Hands on the front thigh");
        lunge.Levels[2].Description.Should().Be("Lift the back knee (high lunge)");
        lunge.Levels[1].Cues.Should().Contain("Back knee down, tail tucked");
        lunge.PerSide.Should().BeTrue();
    }

    [Fact]
    public void Reverse_plank_ladder_has_three_levels_of_3_x_15_to_30_s_and_starts_at_level_1()
    {
        var ladder = Drill("Upper back & chest", "Reverse plank ladder");

        ladder.Levels!.Select(l => l.Description).Should().Equal(
            "Reverse tabletop, knees bent", "Hands on a bench or sofa edge, legs straight", "Full reverse plank on the floor");
        ladder.Levels.Should().OnlyContain(l => l.Sets == 3 && l.Range == new DoseRangeDto(15, 30, 5) && l.Dose.Unit == DoseUnit.Seconds);
        ladder.CurrentLevel.Should().Be(0);
        ladder.Dose.Amount.Should().Be(15);
        ladder.Cues.Should().NotContain("Level 1:");
    }

    [Theory]
    [InlineData("Side plank", 20, 30)]
    [InlineData("Copenhagen plank, short lever", 15, 20)]
    public void Hold_ranges_from_the_doc(string drill, int min, int max)
    {
        var d = Drill("Hips & lower back", drill);

        d.Levels.Should().OnlyContain(l => l.Range == new DoseRangeDto(min, max, 5));
        d.Dose.Amount.Should().Be(min);
    }

    [Fact]
    public void Drills_with_levels_build_valid_domain_drills_and_no_extra_sets()
    {
        var drills = Spec().Routines.SelectMany(r => r.Drills).ToList();

        drills.Select(d => d.ToDomain()).Should().HaveCount(drills.Count);
        drills.Should().OnlyContain(d => d.ExtraSetSide == SideBias.None);
    }

    [Fact]
    public void Saturday_flow_says_a_follow_along_video_can_replace_it()
    {
        var saturday = Spec().Schedule.Single(s => s.Day == DayOfWeek.Saturday && s.Kind == SessionKind.Mobility);

        saturday.Label.Should().Contain("follow-along video");
    }

    [Fact]
    public async Task Applying_creates_routines_with_their_kinds_and_levels()
    {
        var joe = new UserId("user_template_levels");
        var routines = Substitute.For<IMobilityRoutineRepository>();
        var created = new List<MobilityRoutine>();
        routines.ListForUserAsync(joe, Arg.Any<CancellationToken>()).Returns(new List<MobilityRoutine>());
        routines.AddAsync(Arg.Do<MobilityRoutine>(created.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(joe.Value);
        var tracks = Substitute.For<ICardioTrackRepository>();
        tracks.ListForUserAsync(joe, Arg.Any<CancellationToken>()).Returns(new List<CardioTrack>());
        var library = Substitute.For<IExerciseLibraryProvider>();
        library.GetByName(Arg.Any<string>()).Returns(ci => new ExerciseTemplate(ci.Arg<string>(), EquipmentType.Dumbbell, RepRange.Common.Medium, 3, "Test", "chest", []));
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        var handler = new ApplyPlanTemplateCommandHandler(Substitute.For<ITrainingPlanRepository>(), Substitute.For<IWorkoutRepository>(),
            Substitute.For<ICardioProtocolRepository>(), routines, Substitute.For<IHabitRepository>(), library, Substitute.For<IUnitOfWork>(), user, clock, tracks);

        var result = await handler.Handle(new ApplyPlanTemplateCommand(PlanV3BaseBuildTransition.TemplateKey, new DateOnly(2026, 11, 25),
            new Dictionary<string, decimal?>(), null, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        created.Single(r => r.Name == "Full-body flow").Kind.Should().Be(MobilityRoutineKind.YogaFlow);
        created.Single(r => r.Name == "Warm-up (upper)").Kind.Should().Be(MobilityRoutineKind.WarmUp);
        var ladder = created.Single(r => r.Name == "Upper back & chest").Drills.Single(d => d.Name == "Reverse plank ladder");
        ladder.CurrentLevelDetails!.Name.Should().Be("Level 1");
        ladder.Levels.Should().HaveCount(3);
    }
}
