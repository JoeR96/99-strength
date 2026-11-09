using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Personal API tokens. Tokens are stored and looked up by hash only.</summary>
public interface IApiTokenRepository : IRepository<ApiToken, ApiTokenId>
{
    /// <summary>Every token the user has created, revoked ones included, newest first.</summary>
    Task<IReadOnlyList<ApiToken>> ListForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>How many of the user's tokens are not revoked.</summary>
    Task<int> CountActiveForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>The token with this hash (see <see cref="ApiToken.HashOf"/>), revoked or not, or null.</summary>
    Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
}
