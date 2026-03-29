using A2S.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace A2S.Tests.Shared;

/// <summary>
/// Simplified DbContext for testing that includes User entity.
/// Excludes complex Workout aggregates that have EF Core configuration issues.
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> AppUsers => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();
            builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
            builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
            builder.Property(u => u.CreatedAt).IsRequired();
            builder.HasIndex(u => u.Email).IsUnique();
        });
    }
}
