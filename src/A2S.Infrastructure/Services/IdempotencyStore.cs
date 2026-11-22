using A2S.Application.Idempotency;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Services;

/// <summary>EF-backed <see cref="IIdempotencyStore"/> over the <c>IdempotencyRecords</c> table.</summary>
public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly A2SDbContext _db;
    private readonly ILocalClock _clock;

    public IdempotencyStore(A2SDbContext db, ILocalClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IdempotentResponse?> FindAsync(string userId, string key, CancellationToken cancellationToken)
    {
        var cutoff = _clock.NowUtc() - IIdempotencyStore.Lifetime;
        var record = await _db.Set<IdempotencyRecord>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key && r.CreatedAtUtc >= cutoff, cancellationToken);
        return record is null ? null : new IdempotentResponse(record.RequestHash, record.StatusCode, record.ResponseBody);
    }

    public async Task SaveAsync(string userId, string key, string route, IdempotentResponse response, CancellationToken cancellationToken)
    {
        var now = _clock.NowUtc();
        var cutoff = now - IIdempotencyStore.Lifetime;
        var set = _db.Set<IdempotencyRecord>();

        // Expired rows (this user's, including an old row under the same key) make way for the new one.
        await set.Where(r => r.UserId == userId && r.CreatedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);

        // ON CONFLICT DO NOTHING: two first attempts racing on one key keep the first answer stored.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "IdempotencyRecords" ("UserId", "Key", "Route", "RequestHash", "StatusCode", "ResponseBody", "CreatedAtUtc")
            VALUES ({userId}, {key}, {route}, {response.RequestHash}, {response.StatusCode}, {response.Body}, {now})
            ON CONFLICT ("UserId", "Key") DO NOTHING
            """, cancellationToken);
    }
}
