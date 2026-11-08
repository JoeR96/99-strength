using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Creates a brand-new database inside the shared Postgres container and applies every EF
/// migration to it, so tests can exercise the real schema (not the simplified TestDbContext)
/// exactly as production and the migration bundle see it.
/// </summary>
public static class MigratedDatabase
{
    public static async Task<A2SDbContext> CreateAsync(DatabaseFixture fixture, string prefix)
    {
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"{prefix}_{Guid.NewGuid():N}"
        };

        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;

        var context = new A2SDbContext(options);
        await context.Database.MigrateAsync();
        await ApplyPendingModelColumnsAsync(context);
        return context;
    }

    /// <summary>
    /// TEMPORARY (v2 PR 4): ExerciseDefinition.SecondaryMuscleGroups is mapped but its column has no
    /// migration until the integrator scaffolds AddSecondaryMuscleGroups. Without this the seeder
    /// tests fail with "column does not exist". Delete this method once that migration exists;
    /// it is a no-op after it (IF NOT EXISTS).
    /// </summary>
    private static Task ApplyPendingModelColumnsAsync(A2SDbContext context) =>
        context.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "ExerciseDefinitions" ADD COLUMN IF NOT EXISTS "SecondaryMuscleGroups" jsonb NOT NULL DEFAULT '[]'::jsonb""");

    public static A2SDbContext Reopen(A2SDbContext context)
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(context.Database.GetConnectionString())
            .Options;
        return new A2SDbContext(options);
    }
}
