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

        // Use a connection string for design-time only
        optionsBuilder.UseNpgsql("Host=localhost;Database=a2s_dev;Username=postgres;Password=postgres");

        return new A2SDbContext(optionsBuilder.Options);
    }
}
