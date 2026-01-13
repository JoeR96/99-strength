using A2S.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace A2S.Tests.Shared;

/// <summary>
/// Simplified DbContext for testing that only includes Identity tables.
/// Excludes complex Workout aggregates that have EF Core configuration issues.
/// </summary>
public class TestDbContext : IdentityDbContext<ApplicationUser>
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Only configure Identity tables, skip Workout entities for now
        // This allows auth tests to run while we fix the Workout entity configurations
    }
}
