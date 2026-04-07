using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class ProgressionSnapshotTests
{
    private static readonly ExerciseId TestExerciseId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public void Constructor_WhenValidValues_ShouldCreateSuccessfully()
    {
        var snapshot = new ProgressionSnapshot(
            TestExerciseId, "Bench Press", "Linear", """{"TrainingMaxValue":100}""");

        snapshot.ExerciseId.Should().Be(TestExerciseId);
        snapshot.ExerciseName.Should().Be("Bench Press");
        snapshot.ProgressionType.Should().Be("Linear");
        snapshot.ProgressionStateJson.Should().Contain("100");
    }

    [Fact]
    public void FromState_WhenLinearState_ShouldSerializeCorrectly()
    {
        var state = new LinearProgressionState(100m, 1, true, 4);

        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Bench Press", state);

        snapshot.ProgressionType.Should().Be("Linear");
        snapshot.ExerciseName.Should().Be("Bench Press");
        snapshot.ProgressionStateJson.Should().Contain("100");
    }

    [Fact]
    public void FromState_WhenRepsPerSetState_ShouldSerializeCorrectly()
    {
        var state = new RepsPerSetProgressionState(30m, 1, 2, 4, 8, 12, false);

        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Cable Row", state);

        snapshot.ProgressionType.Should().Be("RepsPerSet");
        snapshot.ProgressionStateJson.Should().Contain("30");
    }

    [Fact]
    public void FromState_WhenMinimalSetsState_ShouldSerializeCorrectly()
    {
        var state = new MinimalSetsProgressionState(32m, 1, 3, 40, 2, 10);

        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Face Pull", state);

        snapshot.ProgressionType.Should().Be("MinimalSets");
        snapshot.ProgressionStateJson.Should().Contain("32");
    }

    [Fact]
    public void GetLinearState_WhenTypeIsLinear_ShouldDeserializeCorrectly()
    {
        var originalState = new LinearProgressionState(105m, 1, true, 4);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Squat", originalState);

        var deserialized = snapshot.GetLinearState();

        deserialized.Should().NotBeNull();
        deserialized!.TrainingMaxValue.Should().Be(105m);
        deserialized.TrainingMaxUnit.Should().Be(1);
        deserialized.UseAmrap.Should().BeTrue();
        deserialized.BaseSetsPerExercise.Should().Be(4);
    }

    [Fact]
    public void GetLinearState_WhenTypeIsNotLinear_ShouldReturnNull()
    {
        var state = new RepsPerSetProgressionState(30m, 1, 2, 4, 8, 12, false);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Row", state);

        snapshot.GetLinearState().Should().BeNull();
    }

    [Fact]
    public void GetRepsPerSetState_WhenTypeIsRepsPerSet_ShouldDeserializeCorrectly()
    {
        var originalState = new RepsPerSetProgressionState(35m, 1, 3, 4, 8, 12, true);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Curl", originalState);

        var deserialized = snapshot.GetRepsPerSetState();

        deserialized.Should().NotBeNull();
        deserialized!.CurrentWeight.Should().Be(35m);
        deserialized.CurrentSetCount.Should().Be(3);
        deserialized.IsUnilateral.Should().BeTrue();
    }

    [Fact]
    public void GetRepsPerSetState_WhenTypeIsNotRepsPerSet_ShouldReturnNull()
    {
        var state = new LinearProgressionState(100m, 1, true, 4);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Bench", state);

        snapshot.GetRepsPerSetState().Should().BeNull();
    }

    [Fact]
    public void GetMinimalSetsState_WhenTypeIsMinimalSets_ShouldDeserializeCorrectly()
    {
        var originalState = new MinimalSetsProgressionState(32m, 1, 3, 40, 2, 10);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Pull", originalState);

        var deserialized = snapshot.GetMinimalSetsState();

        deserialized.Should().NotBeNull();
        deserialized!.CurrentWeight.Should().Be(32m);
        deserialized.TargetTotalReps.Should().Be(40);
    }

    [Fact]
    public void GetMinimalSetsState_WhenTypeIsNotMinimalSets_ShouldReturnNull()
    {
        var state = new LinearProgressionState(100m, 1, true, 4);
        var snapshot = ProgressionSnapshot.FromState(TestExerciseId, "Bench", state);

        snapshot.GetMinimalSetsState().Should().BeNull();
    }

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var state = new LinearProgressionState(100m, 1, true, 4);
        var s1 = ProgressionSnapshot.FromState(TestExerciseId, "Bench", state);
        var s2 = ProgressionSnapshot.FromState(TestExerciseId, "Bench", state);

        s1.Should().Be(s2);
    }

    [Fact]
    public void Equals_WhenDifferentExerciseId_ShouldNotBeEqual()
    {
        var otherId = new ExerciseId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var state = new LinearProgressionState(100m, 1, true, 4);
        var s1 = ProgressionSnapshot.FromState(TestExerciseId, "Bench", state);
        var s2 = ProgressionSnapshot.FromState(otherId, "Bench", state);

        s1.Should().NotBe(s2);
    }

    [Fact]
    public void Equals_WhenDifferentProgressionType_ShouldNotBeEqual()
    {
        var linearState = new LinearProgressionState(100m, 1, true, 4);
        var rpsState = new RepsPerSetProgressionState(100m, 1, 2, 4, 8, 12, false);

        var s1 = ProgressionSnapshot.FromState(TestExerciseId, "A", linearState);
        var s2 = ProgressionSnapshot.FromState(TestExerciseId, "A", rpsState);

        s1.Should().NotBe(s2);
    }
}
