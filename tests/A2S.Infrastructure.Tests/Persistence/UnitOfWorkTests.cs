using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Persistence;

[Collection("Database")]
public class UnitOfWorkTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _dbContext = null!;
    private UnitOfWork _unitOfWork = null!;

    private static readonly UserId TestUserId = new("uow-test-1111-1111-1111-111111111111");

    public UnitOfWorkTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = CreateDbContext();
        _unitOfWork = new UnitOfWork(_dbContext);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(@"
                DO $$
                DECLARE r RECORD;
                BEGIN
                    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public')
                    LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' CASCADE';
                    END LOOP;
                END $$;");
        }
        catch
        {
            // Ignore cleanup errors
        }
        _unitOfWork.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistTrackedEntities()
    {
        var workout = CreateWorkout();
        _dbContext.Workouts.Add(workout);

        var changes = await _unitOfWork.SaveChangesAsync();

        changes.Should().BeGreaterThan(0);
        await using var verifyContext = CreateDbContext();
        var saved = await verifyContext.Workouts.FindAsync(workout.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CommitTransactionAsync_ShouldPersistData()
    {
        await _unitOfWork.BeginTransactionAsync();

        var workout = CreateWorkout();
        _dbContext.Workouts.Add(workout);

        await _unitOfWork.CommitTransactionAsync();

        await using var verifyContext = CreateDbContext();
        var saved = await verifyContext.Workouts.FindAsync(workout.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldDiscardChanges()
    {
        var workout = CreateWorkout();

        await _unitOfWork.BeginTransactionAsync();
        _dbContext.Workouts.Add(workout);
        await _dbContext.SaveChangesAsync();
        await _unitOfWork.RollbackTransactionAsync();

        await using var verifyContext = CreateDbContext();
        var saved = await verifyContext.Workouts.FindAsync(workout.Id);
        saved.Should().BeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenAlreadyInTransaction_ShouldThrow()
    {
        await _unitOfWork.BeginTransactionAsync();

        var act = () => _unitOfWork.BeginTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");

        await _unitOfWork.RollbackTransactionAsync();
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenNoTransaction_ShouldThrow()
    {
        var act = () => _unitOfWork.CommitTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No transaction*");
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenNoTransaction_ShouldThrow()
    {
        var act = () => _unitOfWork.RollbackTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No transaction*");
    }

    [Fact]
    public async Task MultipleTransactions_SequentiallyWork()
    {
        // First transaction
        await _unitOfWork.BeginTransactionAsync();
        var workout1 = CreateWorkout();
        _dbContext.Workouts.Add(workout1);
        await _unitOfWork.CommitTransactionAsync();

        // Second transaction on same UoW (after first completes)
        await _unitOfWork.BeginTransactionAsync();
        var workout2 = CreateWorkout();
        _dbContext.Workouts.Add(workout2);
        await _unitOfWork.CommitTransactionAsync();

        await using var verifyContext = CreateDbContext();
        var count = await verifyContext.Workouts.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CommitTransactionAsync_AfterCommit_TransactionIsCleared()
    {
        await _unitOfWork.BeginTransactionAsync();
        var workout = CreateWorkout();
        _dbContext.Workouts.Add(workout);
        await _unitOfWork.CommitTransactionAsync();

        // Second commit should fail because transaction was cleared
        var act = () => _unitOfWork.CommitTransactionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No transaction*");
    }

    [Fact]
    public async Task RollbackTransactionAsync_AfterRollback_TransactionIsCleared()
    {
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.RollbackTransactionAsync();

        var act = () => _unitOfWork.RollbackTransactionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No transaction*");
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        var act = () => new UnitOfWork(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private A2SDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new A2SDbContext(options);
    }

    private static Workout CreateWorkout()
    {
        return new WorkoutBuilder()
            .WithUserId(TestUserId)
            .WithName("UoW Test Workout")
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();
    }
}
