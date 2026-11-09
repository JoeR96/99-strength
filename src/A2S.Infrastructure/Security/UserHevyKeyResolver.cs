using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Infrastructure.Security;

/// <summary>
/// Loads the user and unprotects the stored Hevy API key.
/// </summary>
public sealed class UserHevyKeyResolver : IHevyKeyResolver
{
    private readonly IUserRepository _users;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUserService _currentUser;

    public UserHevyKeyResolver(
        IUserRepository users,
        ISecretProtector protector,
        ICurrentUserService currentUser)
    {
        _users = users;
        _protector = protector;
        _currentUser = currentUser;
    }

    public async Task<string?> ForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user?.RevealHevyApiKey(_protector);
    }

    public Task<string?> ForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        return string.IsNullOrEmpty(userId)
            ? Task.FromResult<string?>(null)
            : ForUserAsync(new UserId(userId), cancellationToken);
    }
}
