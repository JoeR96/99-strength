using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Creates a brand-new database inside the shared Postgres container from the current EF model
/// (<c>EnsureCreated</c>, no migrations). For entities whose migration the integrator has not yet
/// generated; once it exists, prefer <see cref="MigratedDatabase"/>.
/// </summary>
public static class FreshDatabase
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
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
