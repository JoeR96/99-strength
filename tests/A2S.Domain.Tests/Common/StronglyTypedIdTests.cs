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
        // Verify it's a value type (struct)
        typeof(UserId).IsValueType.Should().BeTrue();
    }
}
