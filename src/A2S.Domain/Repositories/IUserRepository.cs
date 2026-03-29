using A2S.Domain.Common;
using A2S.Domain.Entities;

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
}
