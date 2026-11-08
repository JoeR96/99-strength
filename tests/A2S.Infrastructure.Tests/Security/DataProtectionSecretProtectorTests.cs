using A2S.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2S.Infrastructure.Tests.Security;

public class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector Create(IDataProtectionProvider? provider = null) =>
        new(provider ?? new EphemeralDataProtectionProvider(), NullLogger<DataProtectionSecretProtector>.Instance);

    [Fact]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        var protector = Create();

        var stored = protector.Protect("hevy-key-abcdef-1234");

        protector.Unprotect(stored).Should().Be("hevy-key-abcdef-1234");
    }

    [Fact]
    public void Protect_DoesNotContainPlaintext()
    {
        var protector = Create();

        var stored = protector.Protect("hevy-key-abcdef-1234");

        stored.Should().NotBe("hevy-key-abcdef-1234");
        stored.Should().NotContain("abcdef");
    }

    [Fact]
    public void Unprotect_WithGarbage_ReturnsNull()
    {
        var protector = Create();

        protector.Unprotect("not-a-protected-payload").Should().BeNull();
    }

    [Fact]
    public void Unprotect_WithValueFromAnotherKeyRing_ReturnsNull()
    {
        var stored = Create(new EphemeralDataProtectionProvider()).Protect("hevy-key-abcdef-1234");

        Create(new EphemeralDataProtectionProvider()).Unprotect(stored).Should().BeNull();
    }

    [Fact]
    public void Unprotect_WithEmpty_ReturnsNull()
    {
        Create().Unprotect("").Should().BeNull();
    }
}
