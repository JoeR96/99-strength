using A2S.Domain.Common;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for User aggregate.
/// </summary>
public class UserRepository : Repository<User, UserId>, IUserRepository
{
    public UserRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await Context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<UserId>> ListIdsWithHevyKeyAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Users
            .Where(u => u.HevyApiKeyProtected != null)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
