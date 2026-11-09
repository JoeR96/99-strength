using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.SeedData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace A2S.Infrastructure.Tests.SeedData;

/// <summary>
/// Runs against a fresh EnsureCreated database (not MigratedDatabase) because the
/// SecondaryMuscleGroups column has no migration yet; the integrator scaffolds it.
/// </summary>
[Collection("Database")]
public class MuscleGroupBackfillTests
{
    private readonly DatabaseFixture _fixture;

    public MuscleGroupBackfillTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Run_OnSeededLibrary_TagsEveryExerciseFromTheHevyToken()
    {
        await using var db = await CreateFreshDatabaseAsync();
        await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);

        var updated = await MuscleGroupBackfill.RunAsync(db, NullLogger.Instance);

        updated.Should().Be(445, "every Hevy-described row starts as 'General'; the five Phase 7 correctives carry their group in the JSON and are skipped");
        await using var fresh = Reopen(db);
        var squat = await fresh.ExerciseDefinitions.SingleAsync(d => d.Name == "Squat (Barbell)");
        squat.MuscleGroup.Should().Be("quadriceps");
        squat.SecondaryMuscleGroups.Should().Equal("hamstrings", "glutes");
        var curl = await fresh.ExerciseDefinitions.SingleAsync(d => d.Name == "21s Bicep Curl");
        curl.MuscleGroup.Should().Be("biceps");
        curl.SecondaryMuscleGroups.Should().BeEmpty();
        (await fresh.ExerciseDefinitions.CountAsync(d => d.MuscleGroup == "General")).Should().Be(0);
    }

    [Fact]
    public async Task Run_Twice_UpdatesNothingTheSecondTime()
    {
        await using var db = await CreateFreshDatabaseAsync();
        await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);
        await MuscleGroupBackfill.RunAsync(db, NullLogger.Instance);

        var second = await MuscleGroupBackfill.RunAsync(Reopen(db), NullLogger.Instance);

        second.Should().Be(0);
    }

    [Fact]
    public async Task Run_OnlyTouchesRowsWhoseGroupsDiffer()
    {
        await using var db = await CreateFreshDatabaseAsync();
        await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);
        var squat = await db.ExerciseDefinitions.SingleAsync(d => d.Name == "Squat (Barbell)");
        squat.SetMuscleGroups("quadriceps", ["hamstrings", "glutes"]);
        await db.SaveChangesAsync();

        var updated = await MuscleGroupBackfill.RunAsync(Reopen(db), NullLogger.Instance);

        updated.Should().Be(444);
    }

    private async Task<A2SDbContext> CreateFreshDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"musclegroups_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<A2SDbContext>().UseNpgsql(builder.ConnectionString).Options;
        var context = new A2SDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static A2SDbContext Reopen(A2SDbContext context) => MigratedDatabase.Reopen(context);
}
