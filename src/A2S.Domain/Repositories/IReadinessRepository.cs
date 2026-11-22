using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Daily readiness, one entry per user per local date.</summary>
public interface IReadinessRepository : IRepository<ReadinessEntry, ReadinessEntryId>
{
    /// <summary>The entry on <paramref name="date"/>, or null.</summary>
    Task<ReadinessEntry?> GetForDateAsync(UserId userId, DateOnly date, CancellationToken ct = default);
}
