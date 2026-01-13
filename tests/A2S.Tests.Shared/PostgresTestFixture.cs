using Testcontainers.PostgreSql;
using Xunit;

namespace A2S.Tests.Shared;

/// <summary>
/// Shared PostgreSQL test container fixture for integration tests.
/// Implements IAsyncLifetime to manage container lifecycle.
/// </summary>
public class PostgresTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the PostgreSQL container before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("a2s_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();
        ConnectionString = _postgresContainer.GetConnectionString();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container after tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a SQL command against the test database.
    /// Useful for seeding test data or cleaning up between tests.
    /// </summary>
    public async Task ExecuteSqlAsync(string sql)
    {
        if (_postgresContainer == null)
        {
            throw new InvalidOperationException("Container has not been initialized.");
        }

        await _postgresContainer.ExecScriptAsync(sql);
    }
}
