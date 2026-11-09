using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.ApiTokens;

/// <summary>
/// A personal API token: a long-lived, revocable credential a user hands to a program that cannot
/// hold a browser session (the desktop habit widget). The token string is
/// <c>nns_pat_</c> + 43 base64url characters (32 random bytes). Only its SHA-256 hash is kept, so the
/// plaintext exists exactly once, in the <see cref="IssuedApiToken"/> returned by <see cref="Issue"/>.
/// </summary>
public sealed class ApiToken : AggregateRoot<ApiTokenId>
{
    public const string TokenPrefix = "nns_pat_";
    public const int SecretBytes = 32;
    public const int SecretLength = 43;
    public const int TokenLength = 51;
    public const int DisplayPrefixLength = 12;
    public const int HashLength = 64;
    public const int MaxNameLength = 50;
    public const int MaxActivePerUser = 10;

    /// <summary>Last-used is refreshed at most this often, so a busy client does not cost a write per request.</summary>
    public static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    private ApiToken(ApiTokenId id, UserId userId, string name, string tokenHash, string prefix, DateTimeOffset createdAtUtc)
        : base(id)
    {
        UserId = userId;
        Name = name;
        TokenHash = tokenHash;
        Prefix = prefix;
        CreatedAtUtc = createdAtUtc;
    }

    // EF Core constructor
    private ApiToken()
    {
        Name = string.Empty;
        TokenHash = string.Empty;
        Prefix = string.Empty;
    }

    public UserId UserId { get; private set; }

    /// <summary>The label the user chose, for example "Terran CC".</summary>
    public string Name { get; private set; }

    /// <summary>Lower-case hex SHA-256 of the whole token string. Never the token itself.</summary>
    public string TokenHash { get; private set; }

    /// <summary>The first characters of the token, shown so the user can tell tokens apart.</summary>
    public string Prefix { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    /// <summary>
    /// Mints a token for <paramref name="userId"/>. <paramref name="activeTokenCount"/> is how many
    /// unrevoked tokens the user already has; the cap is <see cref="MaxActivePerUser"/>.
    /// </summary>
    public static IssuedApiToken Issue(UserId userId, string name, int activeTokenCount, DateTimeOffset now)
    {
        CheckRule(
            activeTokenCount < MaxActivePerUser,
            $"You can have at most {MaxActivePerUser} active tokens. Revoke one you no longer use first.");
        var cleanName = CheckName(name);

        var plaintext = TokenPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));
        var token = new ApiToken(
            new ApiTokenId(Guid.NewGuid()),
            userId,
            cleanName,
            HashOf(plaintext),
            plaintext[..DisplayPrefixLength],
            now.ToUniversalTime());

        return new IssuedApiToken(token, plaintext);
    }

    /// <summary>Stops the token working. Returns false when it was already revoked (the first time stands).</summary>
    public bool Revoke(DateTimeOffset now)
    {
        if (RevokedAtUtc is not null)
        {
            return false;
        }

        RevokedAtUtc = now.ToUniversalTime();
        return true;
    }

    /// <summary>
    /// Notes that the token was just used. Returns true when the stored time moved, which happens at
    /// most once per <see cref="LastUsedResolution"/>; callers only save on true.
    /// </summary>
    public bool MarkUsed(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        if (LastUsedAtUtc is { } last && utc - last < LastUsedResolution)
        {
            return false;
        }

        LastUsedAtUtc = utc;
        return true;
    }

    /// <summary>Whether <paramref name="presented"/> is this token, compared in constant time on the hashes.</summary>
    public bool Matches(string presented)
    {
        if (!IsWellFormed(presented) || TokenHash.Length != HashLength)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(TokenHash),
            Encoding.ASCII.GetBytes(HashOf(presented)));
    }

    /// <summary>Lower-case hex SHA-256 of a token string: the only form that is stored or looked up.</summary>
    public static string HashOf(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>Whether a string has the shape of a token: the prefix followed by 43 base64url characters.</summary>
    public static bool IsWellFormed(string? value)
    {
        if (value is null || value.Length != TokenLength || !value.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in value.AsSpan(TokenPrefix.Length))
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string CheckName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        CheckRule(trimmed.Length > 0, "A token needs a name.");
        CheckRule(trimmed.Length <= MaxNameLength, $"A token name cannot exceed {MaxNameLength} characters.");
        return trimmed;
    }
}

/// <summary>
/// A freshly minted token together with its plaintext. This is the only place the plaintext exists:
/// hand it to the user once and let it go. Deliberately not a record, so it has no generated
/// <c>ToString</c> that could print the secret.
/// </summary>
public sealed class IssuedApiToken
{
    public IssuedApiToken(ApiToken token, string plaintext)
    {
        Token = token;
        Plaintext = plaintext;
    }

    public ApiToken Token { get; }

    public string Plaintext { get; }
}
