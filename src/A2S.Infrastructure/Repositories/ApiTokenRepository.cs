using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for personal API tokens.</summary>
public class ApiTokenRepository : Repository<ApiToken, ApiTokenId>, IApiTokenRepository
{
    public ApiTokenRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<ApiToken>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.ApiTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<int> CountActiveForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return Context.ApiTokens.CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null, ct);
    }

    public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return Context.ApiTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }
}
