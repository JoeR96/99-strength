using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.ApiTokens;

public class ApiTokenTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_MintsThePrefixPlus43Base64UrlCharacters()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);

        issued.Plaintext.Should().MatchRegex("^nns_pat_[A-Za-z0-9_-]{43}$");
        issued.Plaintext.Should().HaveLength(ApiToken.TokenLength);
        ApiToken.IsWellFormed(issued.Plaintext).Should().BeTrue();
    }

    [Fact]
    public void Issue_StoresOnlyTheSha256HexOfTheWholeToken()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);

        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Plaintext)));
        issued.Token.TokenHash.Should().Be(expected);
        issued.Token.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        ApiToken.HashOf(issued.Plaintext).Should().Be(expected);
    }

    [Fact]
    public void Issue_KeepsNoPlaintextOnTheAggregate()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        var secret = issued.Plaintext[ApiToken.DisplayPrefixLength..];

        var strings = typeof(ApiToken)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string?)f.GetValue(issued.Token))
            .ToList();

        strings.Should().NotBeEmpty();
        strings.Should().NotContain(s => s != null && s.Contains(secret, StringComparison.Ordinal));
        issued.ToString().Should().NotContain(secret);
        issued.Token.ToString().Should().NotContain(secret);
    }

    [Fact]
    public void Issue_SetsPrefixNameOwnerAndCreationTime()
    {
        var issued = ApiToken.Issue(Joe, "  Terran CC  ", 0, new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(1)));

        issued.Token.Prefix.Should().Be(issued.Plaintext[..12]);
        issued.Token.Prefix.Should().StartWith("nns_pat_");
        issued.Token.Name.Should().Be("Terran CC");
        issued.Token.UserId.Should().Be(Joe);
        issued.Token.CreatedAtUtc.Should().Be(At);
        issued.Token.LastUsedAtUtc.Should().BeNull();
        issued.Token.RevokedAtUtc.Should().BeNull();
        issued.Token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Issue_NeverRepeatsAToken()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => ApiToken.Issue(Joe, "t", 0, At).Plaintext).ToList();

        tokens.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Issue_RejectsAMissingName(string? name)
    {
        var act = () => ApiToken.Issue(Joe, name!, 0, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Issue_AcceptsFiftyCharacters_RejectsFiftyOne()
    {
        var fifty = new string('a', 50);

        ApiToken.Issue(Joe, fifty, 0, At).Token.Name.Should().Be(fifty);
        var act = () => ApiToken.Issue(Joe, fifty + "a", 0, At);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Issue_AllowsTheTenth_RejectsTheEleventh()
    {
        ApiToken.Issue(Joe, "tenth", 9, At).Token.Should().NotBeNull();

        var act = () => ApiToken.Issue(Joe, "eleventh", 10, At);
        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*at most 10*");
    }

    [Fact]
    public void Revoke_StampsTheTimeOnce()
    {
        var token = ApiToken.Issue(Joe, "Terran CC", 0, At).Token;

        token.Revoke(At.AddHours(1)).Should().BeTrue();
        token.Revoke(At.AddHours(2)).Should().BeFalse();

        token.RevokedAtUtc.Should().Be(At.AddHours(1));
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_MovesAtMostOncePerMinute()
    {
        var token = ApiToken.Issue(Joe, "Terran CC", 0, At).Token;

        token.MarkUsed(At.AddSeconds(5)).Should().BeTrue();
        token.MarkUsed(At.AddSeconds(30)).Should().BeFalse();
        token.MarkUsed(At.AddSeconds(64)).Should().BeFalse();
        token.LastUsedAtUtc.Should().Be(At.AddSeconds(5));

        token.MarkUsed(At.AddSeconds(65)).Should().BeTrue();
        token.LastUsedAtUtc.Should().Be(At.AddSeconds(65));
    }

    [Fact]
    public void Matches_OnlyItsOwnPlaintext()
    {
        var issued = ApiToken.Issue(Joe, "Terran CC", 0, At);
        var other = ApiToken.Issue(Joe, "Other", 1, At);

        issued.Token.Matches(issued.Plaintext).Should().BeTrue();
        issued.Token.Matches(other.Plaintext).Should().BeFalse();
        issued.Token.Matches("nns_pat_short").Should().BeFalse();
        issued.Token.Matches(issued.Token.TokenHash).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nns_pat_")]
    [InlineData("nns_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("nns_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("nns_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("nns_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("NNS_PAT_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("xxx_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void IsWellFormed_RejectsAnythingThatIsNotTheExactShape(string? value)
    {
        ApiToken.IsWellFormed(value).Should().BeFalse();
    }

    [Fact]
    public void IsWellFormed_AcceptsTheWholeBase64UrlAlphabet()
    {
        var value = "nns_pat_" + "AZaz09-_" + new string('b', 35);

        Regex.IsMatch(value, "^nns_pat_[A-Za-z0-9_-]{43}$").Should().BeTrue();
        ApiToken.IsWellFormed(value).Should().BeTrue();
    }
}
