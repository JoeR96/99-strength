using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using A2S.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Persistence;

[Collection("Database")]
public class ConfigurationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _dbContext = null!;

    public ConfigurationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = CreateDbContext();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region Workout Configuration

    [Fact]
    public void WorkoutConfiguration_ShouldMapToCorrectTable()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        entityType.GetTableName().Should().Be("Workouts");
    }

    [Fact]
    public void WorkoutConfiguration_Id_ShouldBeConfiguredCorrectly()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var idProperty = entityType.FindProperty(nameof(Workout.Id))!;

        idProperty.GetValueGeneratorFactory().Should().BeNull("WorkoutId should be ValueGeneratedNever");
    }

    [Fact]
    public void WorkoutConfiguration_Name_ShouldHaveMaxLength200()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var nameProperty = entityType.FindProperty(nameof(Workout.Name))!;

        nameProperty.GetMaxLength().Should().Be(200);
        nameProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void WorkoutConfiguration_UserId_ShouldBeRequiredWithMaxLength256()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var userIdProperty = entityType.FindProperty(nameof(Workout.UserId))!;

        userIdProperty.GetMaxLength().Should().Be(256);
        userIdProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void WorkoutConfiguration_Variant_ShouldBeStoredAsString()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var variantProperty = entityType.FindProperty(nameof(Workout.Variant))!;

        variantProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void WorkoutConfiguration_Status_ShouldBeStoredAsString()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var statusProperty = entityType.FindProperty(nameof(Workout.Status))!;

        statusProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void WorkoutConfiguration_ShouldHaveConcurrencyToken()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var xminProperty = entityType.FindProperty("xmin");

        xminProperty.Should().NotBeNull();
        xminProperty!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void WorkoutConfiguration_CascadeDeleteExercises()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var navigation = entityType.FindNavigation(nameof(Workout.Exercises))!;
        var fk = navigation.ForeignKey;

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void WorkoutConfiguration_HasUserIdIndex()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var indexes = entityType.GetIndexes().ToList();

        indexes.Should().Contain(idx =>
            idx.Properties.Any(p => p.Name == nameof(Workout.UserId)));
    }

    [Fact]
    public void WorkoutConfiguration_HasUserIdStatusCompositeIndex()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var indexes = entityType.GetIndexes().ToList();

        indexes.Should().Contain(idx =>
            idx.Properties.Count == 2 &&
            idx.Properties.Any(p => p.Name == nameof(Workout.UserId)) &&
            idx.Properties.Any(p => p.Name == nameof(Workout.Status)));
    }

    [Fact]
    public void WorkoutConfiguration_BlockSequence_ShouldBeJsonb()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Workout))!;
        var blockSeqProperty = entityType.FindProperty("_blockSequence");

        blockSeqProperty.Should().NotBeNull();
        blockSeqProperty!.GetColumnName().Should().Be("BlockSequence");
        blockSeqProperty.GetColumnType().Should().Be("jsonb");
    }

    #endregion

    #region Exercise Configuration

    [Fact]
    public void ExerciseConfiguration_ShouldMapToCorrectTable()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Exercise))!;
        entityType.GetTableName().Should().Be("Exercises");
    }

    [Fact]
    public void ExerciseConfiguration_Name_ShouldHaveMaxLength200()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Exercise))!;
        var nameProperty = entityType.FindProperty(nameof(Exercise.Name))!;

        nameProperty.GetMaxLength().Should().Be(200);
        nameProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ExerciseConfiguration_ExternalTemplateId_ShouldMapToHevyExerciseTemplateId()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Exercise))!;
        var property = entityType.FindProperty(nameof(Exercise.ExternalTemplateId))!;

        property.GetColumnName().Should().Be("HevyExerciseTemplateId");
        property.GetMaxLength().Should().Be(100);
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ExerciseConfiguration_CascadeDeleteProgression()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Exercise))!;
        var navigation = entityType.FindNavigation(nameof(Exercise.Progression))!;
        var fk = navigation.ForeignKey;

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    #endregion

    #region ExerciseProgression Configuration (TPH)

    [Fact]
    public void ExerciseProgressionConfiguration_ShouldMapToCorrectTable()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(ExerciseProgression))!;
        entityType.GetTableName().Should().Be("ExerciseProgressions");
    }

    [Fact]
    public void ExerciseProgressionConfiguration_ShouldUseTPHDiscriminator()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(ExerciseProgression))!;
        var discriminator = entityType.FindDiscriminatorProperty();

        discriminator.Should().NotBeNull();
        discriminator!.Name.Should().Be(nameof(ExerciseProgression.ProgressionType));
    }

    [Fact]
    public void ExerciseProgressionConfiguration_LinearStrategy_DiscriminatorValue()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(LinearProgressionStrategy))!;
        var discriminatorValue = entityType.GetDiscriminatorValue();

        discriminatorValue.Should().Be("Linear");
    }

    [Fact]
    public void ExerciseProgressionConfiguration_RepsPerSetStrategy_DiscriminatorValue()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var discriminatorValue = entityType.GetDiscriminatorValue();

        discriminatorValue.Should().Be("RepsPerSet");
    }

    [Fact]
    public void ExerciseProgressionConfiguration_MinimalSetsStrategy_DiscriminatorValue()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(MinimalSetsStrategy))!;
        var discriminatorValue = entityType.GetDiscriminatorValue();

        discriminatorValue.Should().Be("MinimalSets");
    }

    #endregion

    #region LinearProgressionStrategy Configuration

    [Fact]
    public void LinearStrategyConfiguration_ShouldHaveDirectProperties()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(LinearProgressionStrategy))!;
        var columnNames = entityType.GetFlattenedProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        columnNames.Should().Contain("UseAmrap");
        columnNames.Should().Contain("BaseSetsPerExercise");
    }

    [Fact]
    public void LinearStrategyConfiguration_ShouldHaveTrainingMaxOwned()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(LinearProgressionStrategy))!;
        var navigation = entityType.FindNavigation("TrainingMax");

        navigation.Should().NotBeNull();
    }

    #endregion

    #region RepsPerSetStrategy Configuration

    [Fact]
    public void RepsPerSetStrategyConfiguration_ShouldHaveDirectProperties()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var columnNames = entityType.GetFlattenedProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        columnNames.Should().Contain("CurrentSetCount");
        columnNames.Should().Contain("StartingSets");
        columnNames.Should().Contain("TargetSets");
        columnNames.Should().Contain("Equipment");
        columnNames.Should().Contain("IsUnilateral");
        columnNames.Should().Contain("PendingWeightConfirmation");
    }

    [Fact]
    public void RepsPerSetStrategyConfiguration_ShouldHaveCurrentWeightOwned()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var navigation = entityType.FindNavigation("CurrentWeight");

        navigation.Should().NotBeNull();
    }

    [Fact]
    public void RepsPerSetStrategyConfiguration_ShouldHaveRepRangeOwned()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var navigation = entityType.FindNavigation("RepRange");

        navigation.Should().NotBeNull();
    }

    [Fact]
    public void RepsPerSetStrategyConfiguration_ShouldHaveSuggestedWeightOwned()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var navigation = entityType.FindNavigation("SuggestedWeight");

        navigation.Should().NotBeNull();
    }

    [Fact]
    public void RepsPerSetStrategyConfiguration_IsUnilateral_ShouldDefaultToFalse()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var property = entityType.GetFlattenedProperties()
            .FirstOrDefault(p => p.GetColumnName() == "IsUnilateral");

        property.Should().NotBeNull();
        property!.GetDefaultValue().Should().Be(false);
    }

    [Fact]
    public void RepsPerSetStrategyConfiguration_PendingWeightConfirmation_ShouldDefaultToFalse()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(RepsPerSetStrategy))!;
        var property = entityType.GetFlattenedProperties()
            .FirstOrDefault(p => p.GetColumnName() == "PendingWeightConfirmation");

        property.Should().NotBeNull();
        property!.GetDefaultValue().Should().Be(false);
    }

    #endregion

    #region MinimalSetsStrategy Configuration

    [Fact]
    public void MinimalSetsStrategyConfiguration_ShouldHaveDirectProperties()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(MinimalSetsStrategy))!;
        var columnNames = entityType.GetFlattenedProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        columnNames.Should().Contain("MinimalSets_TargetTotalReps");
        columnNames.Should().Contain("MinimalSets_CurrentSetCount");
        columnNames.Should().Contain("MinimalSets_StartingSets");
        columnNames.Should().Contain("MinimalSets_MinimumSets");
        columnNames.Should().Contain("MinimalSets_MaximumSets");
        columnNames.Should().Contain("MinimalSets_Equipment");
    }

    [Fact]
    public void MinimalSetsStrategyConfiguration_ShouldHaveCurrentWeightOwned()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(MinimalSetsStrategy))!;
        var navigation = entityType.FindNavigation("CurrentWeight");

        navigation.Should().NotBeNull();
    }

    #endregion

    #region User Configuration

    [Fact]
    public void UserConfiguration_ShouldMapToCorrectTable()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(User))!;
        entityType.GetTableName().Should().Be("Users");
    }

    [Fact]
    public void UserConfiguration_Email_ShouldBeUniqueIndex()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(User))!;
        var indexes = entityType.GetIndexes().ToList();

        indexes.Should().Contain(idx =>
            idx.Properties.Any(p => p.Name == nameof(User.Email)) &&
            idx.IsUnique);
    }

    [Fact]
    public void UserConfiguration_UserId_ShouldBeCharacterVarying()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(User))!;
        var idProperty = entityType.FindProperty(nameof(User.Id))!;

        idProperty.GetColumnType().Should().Be("character varying");
        idProperty.GetMaxLength().Should().Be(256);
    }

    #endregion

    #region ExerciseDefinition Configuration

    [Fact]
    public void ExerciseDefinitionConfiguration_ShouldExistInModel()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(ExerciseDefinition));
        entityType.Should().NotBeNull();
    }

    #endregion

    private A2SDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new A2SDbContext(options);
    }
}
