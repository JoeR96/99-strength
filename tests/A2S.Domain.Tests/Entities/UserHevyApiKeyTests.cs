using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Entities;

public class UserHevyApiKeyTests
{
    private sealed class ReversibleProtector : ISecretProtector
    {
        public string Protect(string plaintext) => "P:" + plaintext;
        public string? Unprotect(string protectedValue) =>
            protectedValue.StartsWith("P:") ? protectedValue[2..] : null;
    }

    private static readonly ISecretProtector Protector = new ReversibleProtector();

    [Fact]
    public void SetHevyApiKey_StoresProtectedValue_NotPlaintext()
    {
        var user = User.Create("key@example.com", "Key User");

        user.SetHevyApiKey("  hevy-secret-1234  ", Protector);

        user.HasHevyApiKey.Should().BeTrue();
        user.HevyApiKeyProtected.Should().Be("P:hevy-secret-1234");
        user.HevyApiKeyProtected.Should().NotContain("  hevy");
    }

    [Fact]
    public void RevealHevyApiKey_RoundTripsThroughProtector()
    {
        var user = User.Create("key@example.com", "Key User");
        user.SetHevyApiKey("hevy-secret-1234", Protector);

        user.RevealHevyApiKey(Protector).Should().Be("hevy-secret-1234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetHevyApiKey_WithBlankValue_ClearsKey(string? blank)
    {
        var user = User.Create("key@example.com", "Key User");
        user.SetHevyApiKey("hevy-secret-1234", Protector);

        user.SetHevyApiKey(blank, Protector);

        user.HasHevyApiKey.Should().BeFalse();
        user.HevyApiKeyProtected.Should().BeNull();
        user.RevealHevyApiKey(Protector).Should().BeNull();
    }

    [Fact]
    public void ClearHevyApiKey_RemovesKey()
    {
        var user = User.Create("key@example.com", "Key User");
        user.SetHevyApiKey("hevy-secret-1234", Protector);

        user.ClearHevyApiKey();

        user.HasHevyApiKey.Should().BeFalse();
        user.HevyApiKeyProtected.Should().BeNull();
    }

    [Fact]
    public void RevealHevyApiKey_WhenNoKey_ReturnsNull()
    {
        var user = User.Create("key@example.com", "Key User");

        user.RevealHevyApiKey(Protector).Should().BeNull();
    }
}
