using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class MuscleGroupsTests
{
    [Theory]
    [InlineData("chest", VolumeBucket.Chest)]
    [InlineData("lats", VolumeBucket.Back)]
    [InlineData("upper_back", VolumeBucket.Back)]
    [InlineData("traps", VolumeBucket.Back)]
    [InlineData("lower_back", VolumeBucket.Back)]
    [InlineData("shoulders", VolumeBucket.Shoulders)]
    [InlineData("quadriceps", VolumeBucket.Quads)]
    [InlineData("hamstrings", VolumeBucket.Hamstrings)]
    [InlineData("glutes", VolumeBucket.Glutes)]
    [InlineData("abductors", VolumeBucket.Glutes)]
    [InlineData("adductors", VolumeBucket.Glutes)]
    [InlineData("abdominals", VolumeBucket.Core)]
    [InlineData("biceps", VolumeBucket.Arms)]
    [InlineData("triceps", VolumeBucket.Arms)]
    [InlineData("forearms", VolumeBucket.Arms)]
    [InlineData("calves", VolumeBucket.Calves)]
    [InlineData("neck", VolumeBucket.Other)]
    [InlineData("full_body", VolumeBucket.Other)]
    [InlineData("cardio", VolumeBucket.Other)]
    [InlineData("other", VolumeBucket.Other)]
    public void BucketFor_MapsEveryHevyGroup(string hevyGroup, VolumeBucket expected)
    {
        MuscleGroups.BucketFor(hevyGroup).Should().Be(expected);
    }

    [Theory]
    [InlineData(" Chest ")]
    [InlineData("CHEST")]
    public void BucketFor_IgnoresCaseAndWhitespace(string hevyGroup)
    {
        MuscleGroups.BucketFor(hevyGroup).Should().Be(VolumeBucket.Chest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("General")]
    [InlineData("pecs")]
    public void BucketFor_ReturnsNullForUnknownGroups(string hevyGroup)
    {
        MuscleGroups.BucketFor(hevyGroup).Should().BeNull();
    }
}
