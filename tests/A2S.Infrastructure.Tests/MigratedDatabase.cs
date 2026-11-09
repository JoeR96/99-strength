using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Creates a brand-new database inside the shared Postgres container and applies every EF
/// migration to it, so tests can exercise the real schema (not the simplified TestDbContext)
/// exactly as production and the migration bundle see it.
/// </summary>
public static class MigratedDatabase
{
    /// <param name="fixture">The shared Postgres container.</param>
    /// <param name="prefix">Database name prefix.</param>
    /// <param name="targetMigration">
    /// Stop after this migration instead of applying the whole chain, so a test can seed rows in the
    /// schema an earlier release left behind and then migrate the rest of the way.
    /// </param>
    public static async Task<A2SDbContext> CreateAsync(DatabaseFixture fixture, string prefix, string? targetMigration = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"{prefix}_{Guid.NewGuid():N}"
        };

        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(builder.ConnectionString)
            // As in TestWebApplicationFactory: a PR may add entities before the integrator scaffolds
            // their migration; these tests only need the tables the migrations already create.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new A2SDbContext(options);
        if (targetMigration is null)
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        }

        return context;
    }

    public static A2SDbContext Reopen(A2SDbContext context)
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(context.Database.GetConnectionString())
            .Options;
        return new A2SDbContext(options);
    }
}
