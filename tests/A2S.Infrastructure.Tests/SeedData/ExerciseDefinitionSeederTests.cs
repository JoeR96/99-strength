using A2S.Domain.Enums;
using A2S.Infrastructure.SeedData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2S.Infrastructure.Tests.SeedData;

/// <summary>
/// Pins the fix for the fresh-database bug: the SeedCableCorePallofPress migration inserts one row,
/// and the old seeder then returned early, leaving 444 exercises missing and 77 API tests red.
/// </summary>
[Collection("Database")]
public class ExerciseDefinitionSeederTests
{
    private readonly DatabaseFixture _fixture;

    public ExerciseDefinitionSeederTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seed_OnFreshlyMigratedDatabase_InsertsEveryLibraryEntry()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "seed");
        var library = await ExerciseDefinitionSeeder.LoadLibraryAsync();
        (await db.ExerciseDefinitions.CountAsync()).Should().Be(1, "the Pallof Press data migration already inserted one row");

        var inserted = await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);

        inserted.Should().Be(library.Count - 1);
        await using var fresh = MigratedDatabase.Reopen(db);
        (await fresh.ExerciseDefinitions.CountAsync()).Should().Be(library.Count);
        library.Count.Should().Be(450);
        (await fresh.ExerciseDefinitions.AnyAsync(d => d.Name == "Squat (Barbell)")).Should().BeTrue();
        (await fresh.ExerciseDefinitions.CountAsync(d => d.Name == "Cable Core Pallof Press")).Should().Be(1);
    }

    /// <summary>
    /// Phase 7: the suggested plan's accessories and correctives that Hevy's library does not have.
    /// They carry their muscle group in the JSON itself (no "Hevy:" token), so the seeder tags them
    /// directly and the Hevy backfill leaves them alone.
    /// </summary>
    [Theory]
    [InlineData("Seal Row (Barbell)", "upper_back", EquipmentType.Barbell)]
    [InlineData("Wall Slide with Lift-Off", "shoulders", EquipmentType.Bodyweight)]
    [InlineData("Copenhagen Plank (Short Lever)", "adductors", EquipmentType.Bodyweight)]
    [InlineData("Cable Serratus Punch", "chest", EquipmentType.Cable)]
    [InlineData("Push-Up Plus", "chest", EquipmentType.Bodyweight)]
    public async Task Seed_InsertsTheSuggestedPlanCorrectives_WithTheirMuscleGroup(string name, string muscleGroup, EquipmentType equipment)
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "seed");

        await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);

        await using var fresh = MigratedDatabase.Reopen(db);
        var definition = await fresh.ExerciseDefinitions.SingleAsync(d => d.Name == name);
        definition.MuscleGroup.Should().Be(muscleGroup);
        definition.EquipmentType.Should().Be(equipment);
        definition.SecondaryMuscleGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Seed_RunTwice_InsertsNothingTheSecondTime()
    {
        await using var db = await MigratedDatabase.CreateAsync(_fixture, "seed");
        await ExerciseDefinitionSeeder.SeedAsync(db, NullLogger.Instance);
        var countAfterFirst = await MigratedDatabase.Reopen(db).ExerciseDefinitions.CountAsync();

        var second = await ExerciseDefinitionSeeder.SeedAsync(MigratedDatabase.Reopen(db), NullLogger.Instance);

        second.Should().Be(0);
        (await MigratedDatabase.Reopen(db).ExerciseDefinitions.CountAsync()).Should().Be(countAfterFirst);
    }
}
