namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for an ApiToken (one personal API token of one user).</summary>
public readonly record struct ApiTokenId(Guid Value);
