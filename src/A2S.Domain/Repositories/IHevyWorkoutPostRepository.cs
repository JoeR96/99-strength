using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Gym-mode sessions queued for (or posted to) Hevy. (user, client session id) is unique.</summary>
public interface IHevyWorkoutPostRepository : IRepository<HevyWorkoutPost, HevyWorkoutPostId>
{
    Task<HevyWorkoutPost?> FindByClientSessionAsync(UserId userId, Guid clientSessionId, CancellationToken ct = default);

    /// <summary>Pending posts (and updates pending) whose next attempt is due, oldest first, across all users (the background retry).</summary>
    Task<IReadOnlyList<HevyWorkoutPost>> ListDueAsync(DateTimeOffset now, int take, CancellationToken ct = default);

    /// <summary>A user's posts for a range of local dates (Today's "Not in Hevy yet", the pull's race check).</summary>
    Task<IReadOnlyList<HevyWorkoutPost>> ListForUserAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
