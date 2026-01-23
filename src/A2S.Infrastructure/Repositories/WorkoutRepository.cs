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
public class WorkoutRepository : IWorkoutRepository
{
    private readonly A2SDbContext _context;

    public WorkoutRepository(A2SDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Workout?> GetActiveWorkoutAsync(string userId, CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.UserId == userId && w.Status == WorkoutStatus.Active)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetAllByUserAsync(string userId, CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetByStatusAsync(WorkoutStatus status, CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .Where(w => w.Status == status)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Workout workout, CancellationToken ct = default)
    {
        await _context.Workouts.AddAsync(workout, ct);
    }

    public void Update(Workout workout)
    {
        _context.Workouts.Update(workout);
    }

    public void Remove(Workout workout)
    {
        _context.Workouts.Remove(workout);
    }
}
