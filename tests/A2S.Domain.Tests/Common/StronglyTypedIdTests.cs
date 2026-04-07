using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class StronglyTypedIdTests
{
    [Fact]
    public void UserId_WhenCreatedWithString_ShouldStoreValue()
    {
        var value = "11111111-1111-1111-1111-111111111111";
        var userId = new UserId(value);

        userId.Value.Should().Be(value);
    }

    [Fact]
    public void UserId_WhenComparedWithSameValue_ShouldBeEqual()
    {
        var value = "22222222-2222-2222-2222-222222222222";
        var userId1 = new UserId(value);
        var userId2 = new UserId(value);

        userId1.Should().Be(userId2);
        (userId1 == userId2).Should().BeTrue();
    }

    [Fact]
    public void UserId_WhenComparedWithDifferentValue_ShouldNotBeEqual()
    {
        var userId1 = new UserId("11111111-1111-1111-1111-111111111111");
        var userId2 = new UserId("22222222-2222-2222-2222-222222222222");

        userId1.Should().NotBe(userId2);
        (userId1 != userId2).Should().BeTrue();
    }

    [Fact]
    public void UserId_DefaultValue_ShouldHaveNullValue()
    {
        var userId = default(UserId);

        userId.Value.Should().BeNull();
    }

    [Fact]
    public void UserId_ShouldBeReadonlyRecordStruct()
    {
        typeof(UserId).IsValueType.Should().BeTrue();
    }

    // --- WorkoutId ---

    [Fact]
    public void WorkoutId_WhenCreatedWithGuid_ShouldStoreValue()
    {
        var guid = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var id = new WorkoutId(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void WorkoutId_WhenComparedWithSameValue_ShouldBeEqual()
    {
        var guid = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var id1 = new WorkoutId(guid);
        var id2 = new WorkoutId(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void WorkoutId_WhenComparedWithDifferentValue_ShouldNotBeEqual()
    {
        var id1 = new WorkoutId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var id2 = new WorkoutId(Guid.Parse("44444444-4444-4444-4444-444444444444"));

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void WorkoutId_DefaultValue_ShouldHaveEmptyGuid()
    {
        var id = default(WorkoutId);

        id.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void WorkoutId_ShouldBeReadonlyRecordStruct()
    {
        typeof(WorkoutId).IsValueType.Should().BeTrue();
    }

    // --- ExerciseId ---

    [Fact]
    public void ExerciseId_WhenCreatedWithGuid_ShouldStoreValue()
    {
        var guid = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var id = new ExerciseId(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ExerciseId_WhenComparedWithSameValue_ShouldBeEqual()
    {
        var guid = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var id1 = new ExerciseId(guid);
        var id2 = new ExerciseId(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void ExerciseId_WhenComparedWithDifferentValue_ShouldNotBeEqual()
    {
        var id1 = new ExerciseId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var id2 = new ExerciseId(Guid.Parse("66666666-6666-6666-6666-666666666666"));

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void ExerciseId_DefaultValue_ShouldHaveEmptyGuid()
    {
        var id = default(ExerciseId);

        id.Value.Should().Be(Guid.Empty);
    }

    // --- ExerciseDefinitionId ---

    [Fact]
    public void ExerciseDefinitionId_WhenCreatedWithGuid_ShouldStoreValue()
    {
        var guid = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var id = new ExerciseDefinitionId(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ExerciseDefinitionId_WhenComparedWithSameValue_ShouldBeEqual()
    {
        var guid = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var id1 = new ExerciseDefinitionId(guid);
        var id2 = new ExerciseDefinitionId(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void ExerciseDefinitionId_WhenComparedWithDifferentValue_ShouldNotBeEqual()
    {
        var id1 = new ExerciseDefinitionId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        var id2 = new ExerciseDefinitionId(Guid.Parse("88888888-8888-8888-8888-888888888888"));

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void ExerciseDefinitionId_DefaultValue_ShouldHaveEmptyGuid()
    {
        var id = default(ExerciseDefinitionId);

        id.Value.Should().Be(Guid.Empty);
    }

    // --- ExerciseProgressionId ---

    [Fact]
    public void ExerciseProgressionId_WhenCreatedWithGuid_ShouldStoreValue()
    {
        var guid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var id = new ExerciseProgressionId(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ExerciseProgressionId_WhenComparedWithSameValue_ShouldBeEqual()
    {
        var guid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var id1 = new ExerciseProgressionId(guid);
        var id2 = new ExerciseProgressionId(guid);

        id1.Should().Be(id2);
    }

    [Fact]
    public void ExerciseProgressionId_WhenComparedWithDifferentValue_ShouldNotBeEqual()
    {
        var id1 = new ExerciseProgressionId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var id2 = new ExerciseProgressionId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        id1.Should().NotBe(id2);
    }
}
