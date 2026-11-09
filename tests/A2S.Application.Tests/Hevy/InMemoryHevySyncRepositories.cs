using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.Tests.Hevy;

/// <summary>A list-backed <see cref="IHevyWorkoutSnapshotRepository"/> for handler tests.</summary>
public sealed class InMemorySnapshots : IHevyWorkoutSnapshotRepository
{
    private readonly List<HevyWorkoutSnapshot> _items = new();

    public IReadOnlyList<HevyWorkoutSnapshot> All => _items;

    public Task<HevyWorkoutSnapshot?> GetByIdAsync(HevyWorkoutId id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(s => s.Id == id));

    public Task AddAsync(HevyWorkoutSnapshot entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(HevyWorkoutSnapshot entity)
    {
    }

    public void Remove(HevyWorkoutSnapshot entity) => _items.Remove(entity);

    public Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAsync(UserId userId, SnapshotStatus status, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HevyWorkoutSnapshot>>(_items.Where(s => s.UserId == userId && s.Status == status).OrderByDescending(s => s.StartUtc).ToList());

    public Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAwaitingMatchAsync(UserId userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HevyWorkoutSnapshot>>(_items.Where(s => s.UserId == userId && s.AwaitsMatching).OrderBy(s => s.StartUtc).ToList());
}

/// <summary>A list-backed <see cref="IHevyPullCursorRepository"/> for handler tests.</summary>
public sealed class InMemoryCursors : IHevyPullCursorRepository
{
    private readonly List<HevyPullCursor> _items = new();

    public IReadOnlyList<HevyPullCursor> All => _items;

    public Task<HevyPullCursor?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(c => c.Id == id));

    public Task AddAsync(HevyPullCursor entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(HevyPullCursor entity)
    {
    }

    public void Remove(HevyPullCursor entity) => _items.Remove(entity);
}
