using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Factory for creating A2SDbContext instances at design time (for migrations).
/// </summary>
public class A2SDbContextFactory : IDesignTimeDbContextFactory<A2SDbContext>
{
    public A2SDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<A2SDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("A2S_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "A2S_CONNECTION_STRING environment variable is not set. " +
                "Set it before running EF Core migrations.");

        optionsBuilder.UseNpgsql(connectionString);

        return new A2SDbContext(optionsBuilder.Options);
    }
}
