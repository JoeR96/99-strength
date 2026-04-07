using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

[Collection("Database")]
public class WorkoutRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _dbContext = null!;
    private WorkoutRepository _repository = null!;

    private static readonly UserId TestUserId = new("a0c11111-1111-1111-1111-111111111111");
    private static readonly UserId OtherUserId = new("a0c22222-2222-2222-2222-222222222222");

    public WorkoutRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = CreateDbContext();
        _repository = new WorkoutRepository(_dbContext);
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
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistWorkout()
    {
        var workout = CreateWorkout(TestUserId);

        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var verifyContext = CreateDbContext();
        var saved = await verifyContext.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Id == workout.Id);

        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Workout");
        saved.UserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnWorkoutWithExercisesAndProgressions()
    {
        var workout = CreateWorkout(TestUserId);
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var result = await queryRepo.GetByIdAsync(workout.Id);

        result.Should().NotBeNull();
        result!.Exercises.Should().NotBeEmpty();
        result.Exercises.First().Progression.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var result = await queryRepo.GetByIdAsync(new WorkoutId(Guid.Parse("a0c99999-9999-9999-9999-999999999999")));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveWorkoutAsync_ShouldReturnActiveWorkout()
    {
        var workout = CreateWorkout(TestUserId);
        workout.Start();
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var result = await queryRepo.GetActiveWorkoutAsync(TestUserId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(WorkoutStatus.Active);
        result.UserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task GetActiveWorkoutAsync_ShouldNotReturnOtherUsersWorkout()
    {
        var workout = CreateWorkout(TestUserId);
        workout.Start();
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var result = await queryRepo.GetActiveWorkoutAsync(OtherUserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOnlyUserWorkouts()
    {
        var workout1 = CreateWorkout(TestUserId, "Workout 1");
        var workout2 = CreateWorkout(TestUserId, "Workout 2");
        var otherWorkout = CreateWorkout(OtherUserId, "Other Workout");
        await _repository.AddAsync(workout1);
        await _repository.AddAsync(workout2);
        await _repository.AddAsync(otherWorkout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var results = await queryRepo.GetAllAsync(TestUserId);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(w => w.UserId == TestUserId);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldFilterByStatus()
    {
        var activeWorkout = CreateWorkout(TestUserId, "Active");
        activeWorkout.Start();
        var draftWorkout = CreateWorkout(TestUserId, "Draft");
        await _repository.AddAsync(activeWorkout);
        await _repository.AddAsync(draftWorkout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var results = await queryRepo.GetByStatusAsync(TestUserId, WorkoutStatus.Active);

        results.Should().ContainSingle();
        results.First().Status.Should().Be(WorkoutStatus.Active);
    }

    [Fact]
    public async Task GetAllByUserSummaryAsync_ShouldReturnWorkoutsWithExercises()
    {
        var workout = CreateWorkout(TestUserId);
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var queryContext = CreateDbContext();
        var queryRepo = new WorkoutRepository(queryContext);
        var results = await queryRepo.GetAllByUserSummaryAsync(TestUserId);

        results.Should().ContainSingle();
        results.First().Exercises.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Update_ShouldPersistChanges()
    {
        var workout = CreateWorkout(TestUserId);
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var updateContext = CreateDbContext();
        var updateRepo = new WorkoutRepository(updateContext);
        var toUpdate = await updateRepo.GetByIdAsync(workout.Id);
        toUpdate!.Start();
        updateRepo.Update(toUpdate);
        await updateContext.SaveChangesAsync();

        await using var verifyContext = CreateDbContext();
        var verifyRepo = new WorkoutRepository(verifyContext);
        var updated = await verifyRepo.GetByIdAsync(workout.Id);
        updated!.Status.Should().Be(WorkoutStatus.Active);
    }

    [Fact]
    public async Task Remove_ShouldDeleteWorkout()
    {
        var workout = CreateWorkout(TestUserId);
        await _repository.AddAsync(workout);
        await _dbContext.SaveChangesAsync();

        await using var deleteContext = CreateDbContext();
        var deleteRepo = new WorkoutRepository(deleteContext);
        var toDelete = await deleteRepo.GetByIdAsync(workout.Id);
        deleteRepo.Remove(toDelete!);
        await deleteContext.SaveChangesAsync();

        await using var verifyContext = CreateDbContext();
        var verifyRepo = new WorkoutRepository(verifyContext);
        var result = await verifyRepo.GetByIdAsync(workout.Id);
        result.Should().BeNull();
    }

    private A2SDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new A2SDbContext(options);
    }

    private static Workout CreateWorkout(UserId userId, string name = "Test Workout")
    {
        return new WorkoutBuilder()
            .WithUserId(userId)
            .WithName(name)
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", DayNumber.Day1, 1, 100m)
            .Build();
    }
}
