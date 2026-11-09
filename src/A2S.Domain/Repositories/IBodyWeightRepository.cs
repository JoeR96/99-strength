using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>
/// Morning weigh-ins for one user, one per local date.
/// </summary>
public interface IBodyWeightRepository : IRepository<BodyWeightEntry, BodyWeightEntryId>
{
    /// <summary>The entry on <paramref name="date"/>, or null.</summary>
    Task<BodyWeightEntry?> GetForDateAsync(UserId userId, DateOnly date, CancellationToken ct = default);

    /// <summary>Entries with <c>from &lt;= Date &lt;= to</c>, ordered by date.</summary>
    Task<IReadOnlyList<BodyWeightEntry>> ListAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
