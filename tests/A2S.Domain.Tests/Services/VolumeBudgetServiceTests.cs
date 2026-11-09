using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class VolumeBudgetServiceTests
{
    private readonly VolumeBudgetService _service = new(new A2SProgramProvider());

    /// <summary>
    /// Upper A from brief § 5: bench (A2S2 main lift, 5 sets in week 1), chest-supported row 3,
    /// landmine press 3, superset cable row 3 + face pulls 3.
    /// </summary>
    private static Workout UpperA(params Exercise[] extra)
    {
        var builder = new WorkoutBuilder()
            .WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithOrder(1).AsLinear())
            .WithExercise(e => e.WithName("Chest Supported Row").WithCategory(ExerciseCategory.Accessory).WithOrder(2).AsRepsPerSet(startingSets: 3))
            .WithExercise(e => e.WithName("Landmine Press").WithCategory(ExerciseCategory.Accessory).WithOrder(3).AsRepsPerSet(startingSets: 3, isUnilateral: true))
            .WithExercise(e => e.WithName("Single Arm Cable Row").WithCategory(ExerciseCategory.Accessory).WithOrder(4).AsRepsPerSet(startingSets: 3, isUnilateral: true))
            .WithExercise(e => e.WithName("Face Pull").WithCategory(ExerciseCategory.Accessory).WithOrder(5).AsRepsPerSet(startingSets: 3));

        foreach (var exercise in extra)
        {
            builder.WithExercise(exercise);
        }

        return builder.Build();
    }

    private static readonly Dictionary<string, (string Primary, IReadOnlyList<string> Secondary)> Library = new()
    {
        ["Bench Press (Barbell)"] = ("chest", ["triceps", "shoulders"]),
        ["Chest Supported Row"] = ("upper_back", ["biceps"]),
        ["Landmine Press"] = ("shoulders", ["chest", "triceps"]),
        ["Single Arm Cable Row"] = ("lats", ["biceps"]),
        ["Face Pull"] = ("shoulders", ["upper_back"]),
        ["Cable Fly"] = ("chest", [])
    };

    private static (string Primary, IReadOnlyList<string> Secondary)? Lookup(string key) =>
        Library.TryGetValue(key, out var groups) ? groups : null;

    [Fact]
    public void HardSetsPerGroup_CountsPrimaryAsOneAndSecondaryAsHalf_RoundedHalfUpPerBucket()
    {
        var sets = _service.HardSetsPerGroup(UpperA(), weekNumber: 1, Lookup);

        sets.Should().BeEquivalentTo(new Dictionary<VolumeBucket, int>
        {
            [VolumeBucket.Chest] = 7,      // 5 + 1.5 (landmine secondary) = 6.5
            [VolumeBucket.Back] = 8,       // 3 + 3 + 1.5 (face pull secondary) = 7.5
            [VolumeBucket.Shoulders] = 9,  // 2.5 (bench secondary) + 3 + 3 = 8.5
            [VolumeBucket.Arms] = 7        // 2.5 + 1.5 + 1.5 + 1.5 = 7
        });
    }

    [Fact]
    public void HardSetsPerGroup_RoundsAHalfSetUp()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e.WithName("Face Pull").WithCategory(ExerciseCategory.Accessory).AsRepsPerSet(startingSets: 1))
            .Build();

        var sets = _service.HardSetsPerGroup(workout, 1, Lookup);

        sets[VolumeBucket.Back].Should().Be(1);
        sets[VolumeBucket.Shoulders].Should().Be(1);
    }

    [Fact]
    public void HardSetsPerGroup_OnDeloadWeek_CountsFourSetsForTheMainLift()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e.WithName("Bench Press (Barbell)").AsLinear())
            .Build();

        var sets = _service.HardSetsPerGroup(workout, weekNumber: 7, Lookup);

        sets[VolumeBucket.Chest].Should().Be(4);
        sets[VolumeBucket.Arms].Should().Be(2);
        sets[VolumeBucket.Shoulders].Should().Be(2);
    }

    [Fact]
    public void HardSetsPerGroup_SkipsExercisesTheLookupCannotResolve()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e.WithName("Mystery Machine").AsLinear())
            .Build();

        var sets = _service.HardSetsPerGroup(workout, 1, Lookup);

        sets.Should().BeEmpty();
    }

    [Fact]
    public void HardSetsPerGroup_ResolvesByExternalTemplateIdBeforeName()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e.WithName("Unknown Name").WithTemplateId("Face Pull").AsLinear())
            .Build();

        var sets = _service.HardSetsPerGroup(workout, 1, Lookup);

        sets[VolumeBucket.Shoulders].Should().Be(5);
    }

    [Fact]
    public void HardSetsPerGroup_OnlyCountsTheExercisesOfTheWholeWeek_NotPerDay()
    {
        var workout = new WorkoutBuilder()
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithDay(DayNumber.Day1).AsLinear())
            .WithExercise(e => e.WithName("Cable Fly").WithDay(DayNumber.Day3).WithCategory(ExerciseCategory.Accessory).AsRepsPerSet(startingSets: 3))
            .Build();

        var sets = _service.HardSetsPerGroup(workout, 1, Lookup);

        sets[VolumeBucket.Chest].Should().Be(8);
    }

    [Fact]
    public void Warnings_UpperAIsWithinEveryDefaultCap()
    {
        var sets = _service.HardSetsPerGroup(UpperA(), 1, Lookup);

        _service.Warnings(sets, VolumeCaps.Default).Should().BeEmpty();
    }

    [Fact]
    public void Warnings_AFourthChestAccessoryPushesChestOverTheCap_YieldsExactlyOneWarning()
    {
        var cableFly = new ExerciseBuilder()
            .WithName("Cable Fly")
            .WithCategory(ExerciseCategory.Accessory)
            .WithOrder(6)
            .AsRepsPerSet(startingSets: 4)
            .Build();
        var sets = _service.HardSetsPerGroup(UpperA(cableFly), 1, Lookup);

        var warnings = _service.Warnings(sets, VolumeCaps.Default);

        warnings.Should().ContainSingle();
        warnings[0].Bucket.Should().Be(VolumeBucket.Chest);
        warnings[0].Sets.Should().Be(11);
        warnings[0].Cap.Should().Be(10);
        warnings[0].Message.Should().Be("Chest: 11 hard sets this week, cap 10");
    }

    [Fact]
    public void Warnings_IgnoreUncappedBuckets()
    {
        var sets = new Dictionary<VolumeBucket, int>
        {
            [VolumeBucket.Arms] = 40,
            [VolumeBucket.Calves] = 40,
            [VolumeBucket.Other] = 40
        };

        _service.Warnings(sets, VolumeCaps.Default).Should().BeEmpty();
    }

    [Fact]
    public void Warnings_AtTheCapIsNotAWarning()
    {
        var sets = new Dictionary<VolumeBucket, int> { [VolumeBucket.Chest] = 10 };

        _service.Warnings(sets, VolumeCaps.Default).Should().BeEmpty();
    }

    [Fact]
    public void DefaultCaps_MatchTheBrief()
    {
        VolumeCaps.Default.HardSetsPerWeek.Should().BeEquivalentTo(new Dictionary<VolumeBucket, int>
        {
            [VolumeBucket.Chest] = 10,
            [VolumeBucket.Back] = 14,
            [VolumeBucket.Shoulders] = 10,
            [VolumeBucket.Quads] = 10,
            [VolumeBucket.Hamstrings] = 10,
            [VolumeBucket.Glutes] = 12,
            [VolumeBucket.Core] = 12
        });
    }
}
