using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Fixture that manages a PostgreSQL test container for integration tests.
/// Uses xUnit's IAsyncLifetime to properly manage the container lifecycle.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("a2s_integration_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates DbContextOptions for the test database connection.
    /// </summary>
    public DbContextOptions<TContext> CreateDbContextOptions<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }
}
