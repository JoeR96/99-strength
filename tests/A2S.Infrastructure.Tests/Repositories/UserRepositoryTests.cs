using A2S.Domain.Entities;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for UserRepository.
/// Uses TestDbContext with Testcontainers PostgreSQL.
/// </summary>
[Collection("Database")]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TestDbContext _dbContext = null!;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        _dbContext = new TestDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up after each test
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Users\" CASCADE");
        }
        catch
        {
            // Ignore errors during cleanup
        }
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistUserToDatabase()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test User");

        // Act
        await _dbContext.AppUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Assert - use a new context to verify persistence
        await using var verifyContext = CreateNewDbContext();
        var savedUser = await verifyContext.AppUsers.FirstOrDefaultAsync(u => u.Id == user.Id);

        savedUser.Should().NotBeNull();
        savedUser!.Email.Should().Be("test@example.com");
        savedUser.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test User");
        await _dbContext.AppUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await using var queryContext = CreateNewDbContext();
        var result = await queryContext.AppUsers.FirstOrDefaultAsync(u => u.Id == user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenEmailExists()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test User");
        await _dbContext.AppUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await using var queryContext = CreateNewDbContext();
        var result = await queryContext.AppUsers.FirstOrDefaultAsync(u => u.Email == "test@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task UniqueEmailConstraint_ShouldThrowOnDuplicateEmail()
    {
        // Arrange
        var user1 = User.Create("duplicate@example.com", "User 1");
        await _dbContext.AppUsers.AddAsync(user1);
        await _dbContext.SaveChangesAsync();

        // Act
        await using var insertContext = CreateNewDbContext();
        var user2 = User.Create("duplicate@example.com", "User 2");
        await insertContext.AppUsers.AddAsync(user2);

        var act = async () => await insertContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Update_ShouldPersistChanges()
    {
        // Arrange
        var user = User.Create("test@example.com", "Original Name");
        await _dbContext.AppUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        await using var updateContext = CreateNewDbContext();
        var userToUpdate = await updateContext.AppUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        userToUpdate!.UpdateName("Updated Name");
        await updateContext.SaveChangesAsync();

        // Assert
        await using var verifyContext = CreateNewDbContext();
        var updatedUser = await verifyContext.AppUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.Name.Should().Be("Updated Name");
    }

    private TestDbContext CreateNewDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new TestDbContext(options);
    }
}
