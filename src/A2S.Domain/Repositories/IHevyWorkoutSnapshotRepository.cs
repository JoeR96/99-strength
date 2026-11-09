using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Pulled Hevy workouts, keyed by Hevy's workout id.</summary>
public interface IHevyWorkoutSnapshotRepository : IRepository<HevyWorkoutSnapshot, HevyWorkoutId>
{
    /// <summary>The user's snapshots in one status, newest start first.</summary>
    Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAsync(UserId userId, SnapshotStatus status, CancellationToken ct = default);

    /// <summary>Snapshots the matcher should still look at (Pending or Unmatched), oldest start first.</summary>
    Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAwaitingMatchAsync(UserId userId, CancellationToken ct = default);
}
