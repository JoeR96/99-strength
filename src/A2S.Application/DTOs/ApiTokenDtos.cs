using A2S.Domain.Aggregates.ApiTokens;

namespace A2S.Application.DTOs;

/// <summary>A personal API token as Settings lists it. Never the token itself, never its hash.</summary>
public sealed record ApiTokenDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt)
{
    public static ApiTokenDto From(ApiToken token) => new(
        token.Id.Value,
        token.Name,
        token.Prefix,
        token.CreatedAtUtc,
        token.LastUsedAtUtc,
        token.RevokedAtUtc);
}

/// <summary>
/// The create response: the one and only time the plaintext <see cref="Token"/> leaves the server.
/// <c>ToString</c> is overridden so the secret cannot reach a log through the record's generated one.
/// </summary>
public sealed record CreatedApiTokenDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    string Token)
{
    public static CreatedApiTokenDto From(IssuedApiToken issued) => new(
        issued.Token.Id.Value,
        issued.Token.Name,
        issued.Token.Prefix,
        issued.Token.CreatedAtUtc,
        issued.Plaintext);

    public override string ToString() => $"CreatedApiTokenDto {{ Id = {Id}, Name = {Name}, Prefix = {Prefix} }}";
}
