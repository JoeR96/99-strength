using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Workout aggregate root.
/// Handles all persistence operations for workouts.
/// </summary>
public class WorkoutRepository : Repository<Workout, WorkoutId>, IWorkoutRepository
{
    public WorkoutRepository(A2SDbContext context) : base(context)
    {
    }

    public override async Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct = default)
    {
        return await Context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Workout?> GetActiveWorkoutAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.UserId == userId && w.Status == WorkoutStatus.Active)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetAllAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetByStatusAsync(UserId userId, WorkoutStatus status, CancellationToken ct = default)
    {
        return await Context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.UserId == userId && w.Status == status)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetAllByUserSummaryAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.Workouts
            .Include(w => w.Exercises)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }
}
