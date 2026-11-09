using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Local key ↔ Hevy id links. Every read is scoped to a user; (user, kind, local key) is unique.</summary>
public interface IHevyLinkRepository : IRepository<HevyLink, HevyLinkId>
{
    Task<HevyLink?> FindAsync(UserId userId, HevyLinkKind kind, string localKey, CancellationToken ct = default);

    /// <summary>The link pointing at a Hevy id (a routine id on a pulled workout), or null.</summary>
    Task<HevyLink?> FindByHevyIdAsync(UserId userId, HevyLinkKind kind, string hevyId, CancellationToken ct = default);

    Task<IReadOnlyList<HevyLink>> ListAsync(UserId userId, HevyLinkKind kind, CancellationToken ct = default);
}
