using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the A2S application.
/// </summary>
public class A2SDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    /// <summary>Used by EF Core tooling (migrations, design-time). Domain events are not dispatched.</summary>
    public A2SDbContext(DbContextOptions<A2SDbContext> options)
        : base(options)
    {
    }

    public A2SDbContext(DbContextOptions<A2SDbContext> options, IDomainEventDispatcher domainEventDispatcher)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseProgression> ExerciseProgressions => Set<ExerciseProgression>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExerciseDefinition> ExerciseDefinitions => Set<ExerciseDefinition>();

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
        // Collect domain events before save
        var aggregatesWithEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear domain events before save to avoid duplicate dispatching on retry
        foreach (var entity in aggregatesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        // Persist changes
        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch events after successful save
        if (_domainEventDispatcher is not null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
        }

        return result;
    }
}
