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
