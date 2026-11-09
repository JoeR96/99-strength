using System.Text;
using A2S.Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace A2S.Api.Tests.Unit;

/// <summary>
/// appsettings.json ships <c>"Clerk": { "Domain": "" }</c> and <c>"Cors": { "Origins": [] }</c>, so a
/// Production deployment that forgets the environment variables has both keys present but empty.
/// That must stop the boot: with them empty the API reported healthy, answered 401 to every
/// signed-in request and failed every browser preflight (found by the Phase 9 live walkthrough).
/// </summary>
public class RequiredSettingsTests
{
    private const string ShippedDefaults = """{ "Clerk": { "Domain": "" }, "Cors": { "Origins": [] } }""";

    private static IConfiguration Build(string json, params (string Key, string Value)[] overrides)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .AddInMemoryCollection(overrides.Select(o => new KeyValuePair<string, string?>(o.Key, o.Value)))
            .Build();
    }

    [Fact]
    public void ClerkDomain_WithTheShippedEmptyDefault_Throws()
    {
        var act = () => RequiredSettings.ClerkDomain(Build(ShippedDefaults));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Clerk:Domain*required*");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("cosmic-treefrog-30.clerk.accounts.dev")]
    [InlineData("ftp://cosmic-treefrog-30.clerk.accounts.dev")]
    public void ClerkDomain_ThatIsNotAnHttpUrl_Throws(string value)
    {
        var act = () => RequiredSettings.ClerkDomain(Build(ShippedDefaults, ("Clerk:Domain", value)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Clerk:Domain*");
    }

    [Fact]
    public void ClerkDomain_WhenSet_IsReturnedWithoutATrailingSlash()
    {
        var domain = RequiredSettings.ClerkDomain(Build(ShippedDefaults, ("Clerk:Domain", "https://cosmic-treefrog-30.clerk.accounts.dev/")));

        domain.Should().Be("https://cosmic-treefrog-30.clerk.accounts.dev");
    }

    [Fact]
    public void CorsOrigins_WithTheShippedEmptyDefault_Throws()
    {
        var act = () => RequiredSettings.CorsOrigins(Build(ShippedDefaults));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cors:Origins*required*");
    }

    [Fact]
    public void CorsOrigins_WithOnlyBlankEntries_Throws()
    {
        var act = () => RequiredSettings.CorsOrigins(Build(ShippedDefaults, ("Cors:Origins:0", ""), ("Cors:Origins:1", "  ")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cors:Origins*required*");
    }

    [Fact]
    public void CorsOrigins_WhenSet_AreReturnedInOrder_WithoutBlanks()
    {
        var origins = RequiredSettings.CorsOrigins(Build(
            ShippedDefaults,
            ("Cors:Origins:0", "http://localhost:5173"),
            ("Cors:Origins:1", ""),
            ("Cors:Origins:2", "https://99-strength.enjoeneer.dev")));

        origins.Should().Equal("http://localhost:5173", "https://99-strength.enjoeneer.dev");
    }
}
