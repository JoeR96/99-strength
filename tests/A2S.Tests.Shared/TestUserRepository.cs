using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace A2S.Tests.Shared;

/// <summary>
/// Test implementation of IUserRepository that uses TestDbContext.
/// </summary>
public class TestUserRepository : IUserRepository
{
    private readonly TestDbContext _dbContext;

    public TestUserRepository(TestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.AppUsers.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        _dbContext.AppUsers.Update(user);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
