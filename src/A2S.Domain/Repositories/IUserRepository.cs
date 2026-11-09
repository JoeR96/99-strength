using A2S.Domain.Common;
using A2S.Domain.Aggregates.User;

namespace A2S.Domain.Repositories;

/// <summary>
/// Repository interface for User aggregate.
/// Extends generic repository with user-specific query methods.
/// </summary>
public interface IUserRepository : IRepository<User, UserId>
{
    /// <summary>
    /// Gets a user by their email address.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>The ids of every user with a Hevy API key stored (the Hevy poll's audience). Never the keys.</summary>
    Task<IReadOnlyList<UserId>> ListIdsWithHevyKeyAsync(CancellationToken cancellationToken = default);
}
