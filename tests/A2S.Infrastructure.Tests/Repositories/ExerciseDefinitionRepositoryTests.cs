using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

[Collection("Database")]
public class ExerciseDefinitionRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _dbContext = null!;
    private ExerciseDefinitionRepository _repository = null!;

    public ExerciseDefinitionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = CreateDbContext();
        _repository = new ExerciseDefinitionRepository(_dbContext);
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
    public async Task GetAllAsync_ShouldReturnAllDefinitions()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.GetAllAsync();

        results.Should().HaveCount(3);
        results.Should().BeInAscendingOrder(e => e.Name);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenNoneExist()
    {
        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.GetAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByEquipmentType()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.SearchAsync(equipmentType: EquipmentType.Barbell);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => e.EquipmentType == EquipmentType.Barbell);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByMuscleGroup()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.SearchAsync(muscleGroup: "Chest");

        results.Should().ContainSingle();
        results.First().Name.Should().Be("Bench Press");
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterBySearchTerm()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.SearchAsync(searchTerm: "bench");

        results.Should().ContainSingle();
        results.First().Name.Should().Be("Bench Press");
    }

    [Fact]
    public async Task SearchAsync_ShouldCombineFilters()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var results = await queryRepo.SearchAsync(
            equipmentType: EquipmentType.Barbell,
            muscleGroup: "Legs");

        results.Should().ContainSingle();
        results.First().Name.Should().Be("Squat");
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnDefinition_WhenExists()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var result = await queryRepo.GetByNameAsync("Squat");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Squat");
        result.EquipmentType.Should().Be(EquipmentType.Barbell);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnNull_WhenNotFound()
    {
        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var result = await queryRepo.GetByNameAsync("NonExistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchPagedAsync_ShouldReturnPagedResults()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var (items, totalCount) = await queryRepo.SearchPagedAsync(page: 1, pageSize: 2);

        items.Should().HaveCount(2);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchPagedAsync_ShouldReturnSecondPage()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var (items, totalCount) = await queryRepo.SearchPagedAsync(page: 2, pageSize: 2);

        items.Should().ContainSingle();
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchPagedAsync_ShouldFilterAndPage()
    {
        await SeedExerciseDefinitions();

        await using var queryContext = CreateDbContext();
        var queryRepo = new ExerciseDefinitionRepository(queryContext);
        var (items, totalCount) = await queryRepo.SearchPagedAsync(
            equipmentType: EquipmentType.Barbell, page: 1, pageSize: 10);

        items.Should().HaveCount(2);
        totalCount.Should().Be(2);
    }

    private async Task SeedExerciseDefinitions()
    {
        var squat = ExerciseDefinition.Create("Squat", EquipmentType.Barbell, "Legs", true, "Barbell squat");
        var bench = ExerciseDefinition.Create("Bench Press", EquipmentType.Barbell, "Chest", true, "Barbell bench press");
        var pulldown = ExerciseDefinition.Create("Lat Pulldown", EquipmentType.Cable, "Back", true, "Cable lat pulldown");

        await _dbContext.ExerciseDefinitions.AddRangeAsync(squat, bench, pulldown);
        await _dbContext.SaveChangesAsync();
    }

    private A2SDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new A2SDbContext(options);
    }
}
