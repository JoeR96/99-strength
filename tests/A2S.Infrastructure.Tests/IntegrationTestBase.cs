using A2S.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Base class for infrastructure integration tests.
/// Provides access to a test database via PostgreSQL Testcontainer.
/// Uses TestDbContext which only includes Identity tables.
/// </summary>
[Collection("Database")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly DatabaseFixture Fixture;
    protected TestDbContext DbContext { get; private set; } = null!;

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        DbContext = new TestDbContext(options);

        // Ensure database schema is created
        await DbContext.Database.EnsureCreatedAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Clean up data after each test for isolation
        await CleanupDataAsync();
        await DbContext.DisposeAsync();
    }

    /// <summary>
    /// Override to clean up test data after each test.
    /// Default implementation truncates all tables.
    /// </summary>
    protected virtual async Task CleanupDataAsync()
    {
        // Delete data in reverse order of dependencies
        // This ensures referential integrity is maintained during cleanup
        try
        {
            await DbContext.Database.ExecuteSqlRawAsync(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public')
                    LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' CASCADE';
                    END LOOP;
                END $$;
            ");
        }
        catch
        {
            // Ignore errors during cleanup (table might not exist yet)
        }
    }

    /// <summary>
    /// Creates a fresh DbContext instance for scenarios requiring multiple contexts.
    /// </summary>
    protected TestDbContext CreateNewDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        return new TestDbContext(options);
    }
}

/// <summary>
/// Collection definition for database tests.
/// Ensures the database fixture is shared across tests in this collection.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
