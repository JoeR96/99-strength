using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.ApiTokens;

/// <summary>Who a presented personal API token belongs to.</summary>
public sealed record ApiTokenIdentity(UserId UserId, string Email, ApiTokenId TokenId, string TokenName);

/// <summary>
/// Turns a presented token string into the user it belongs to. The API's token authentication scheme
/// calls this once per request; it is not a MediatR request so the plaintext never enters a pipeline.
/// </summary>
public interface IApiTokenAuthenticator
{
    /// <summary>
    /// The owner of <paramref name="presentedToken"/>, or null when it is malformed, unknown or
    /// revoked, or its user no longer exists. Records the use (at most once a minute) on success.
    /// </summary>
    Task<ApiTokenIdentity?> AuthenticateAsync(string? presentedToken, CancellationToken ct = default);
}

public sealed class ApiTokenAuthenticator : IApiTokenAuthenticator
{
    private readonly IApiTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;

    public ApiTokenAuthenticator(
        IApiTokenRepository tokens,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ILocalClock clock)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ApiTokenIdentity?> AuthenticateAsync(string? presentedToken, CancellationToken ct = default)
    {
        if (presentedToken is null || !ApiToken.IsWellFormed(presentedToken))
        {
            return null;
        }

        var token = await _tokens.GetByHashAsync(ApiToken.HashOf(presentedToken), ct);
        if (token is null || !token.Matches(presentedToken) || !token.IsActive)
        {
            return null;
        }

        var user = await _users.GetByIdAsync(token.UserId, ct);
        if (user is null)
        {
            return null;
        }

        if (token.MarkUsed(_clock.NowUtc()))
        {
            _tokens.Update(token);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new ApiTokenIdentity(user.Id, user.Email, token.Id, token.Name);
    }
}
