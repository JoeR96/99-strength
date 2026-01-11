using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the A2S application.
/// </summary>
public class A2SDbContext : DbContext
{
    public A2SDbContext(DbContextOptions<A2SDbContext> options)
        : base(options)
    {
    }

    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseProgression> ExerciseProgressions => Set<ExerciseProgression>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(A2SDbContext).Assembly);
    }

    /// <summary>
    /// Dispatch domain events after saving changes.
    /// This ensures events are only raised after successful persistence.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement domain event dispatcher in Phase 0.3
        // For now, just save changes
        var result = await base.SaveChangesAsync(cancellationToken);

        // Clear domain events after successful save
        var entities = ChangeTracker
            .Entries<AggregateRoot<WorkoutId>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity);

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return result;
    }
}
