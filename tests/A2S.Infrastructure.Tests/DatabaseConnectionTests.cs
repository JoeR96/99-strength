using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests;

/// <summary>
/// Tests to verify database connectivity and basic operations.
/// </summary>
[Collection("Database")]
public class DatabaseConnectionTests : IntegrationTestBase
{
    public DatabaseConnectionTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Database_ShouldBeAccessible()
    {
        // Arrange & Act
        var canConnect = await DbContext.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Database_ShouldExecuteSimpleQuery()
    {
        // Arrange & Act - use ExecuteSqlRaw for raw SQL
        var result = await DbContext.Database.ExecuteSqlRawAsync("SELECT 1");

        // Assert - ExecuteSqlRaw returns number of affected rows, but SELECT returns -1 in PostgreSQL
        // Just verify it doesn't throw
        result.Should().Be(-1);
    }
}
