using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Persistence;

[Collection("Database")]
public class GymPlanFitPersistenceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public GymPlanFitPersistenceTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => _db = await MigratedDatabase.CreateAsync(_fixture, "gymfit");

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Superset_group_extra_set_side_and_pending_training_max_round_trip()
    {
        var pending = Exercise.CreateWithPendingTrainingMax("Front Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "5046D0A9", tier: ProgramTier.Auxiliary);
        var split = new ExerciseBuilder().WithName("Split Squat (Dumbbell)").WithOrder(2).AsRepsPerSet(isUnilateral: true, startingSets: 2, targetSets: 3).Build();
        var abduction = new ExerciseBuilder().WithName("Hip Abduction (Machine)").WithOrder(3).AsRepsPerSet().Build();
        var workout = new WorkoutBuilder().WithUserId("user_gymfit_1").WithVariant(ProgramVariant.FourDay)
            .WithExercise(pending).WithExercise(split).WithExercise(abduction).Build();
        workout.SetExerciseSupersetGroup(split.Id, 3);
        workout.SetExerciseSupersetGroup(abduction.Id, 3);
        workout.SetExerciseExtraSetSide(split.Id, ExtraSetSide.Left);

        var repository = new WorkoutRepository(_db);
        await repository.AddAsync(workout);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(workout.Id);

        var loadedSplit = loaded!.Exercises.Single(e => e.Id == split.Id);
        loadedSplit.SupersetGroup.Should().Be(3);
        loadedSplit.ExtraSetSide.Should().Be(ExtraSetSide.Left);
        loaded.Exercises.Single(e => e.Id == abduction.Id).ExtraSetSide.Should().Be(ExtraSetSide.None);
        var loadedPending = loaded.Exercises.Single(e => e.Id == pending.Id);
        loadedPending.IsTrainingMaxPending.Should().BeTrue();
        ((LinearProgressionStrategy)loadedPending.Progression).Tier.Should().Be(ProgramTier.Auxiliary);
    }

    [Fact]
    public async Task Migration_is_reversible()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddSupersetsExtraSetsPendingTrainingMax", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        var afterDown = await ColumnExistsAsync("Exercises", "SupersetGroup");
        var pendingAfterDown = await ColumnExistsAsync("ExerciseProgressions", "IsTrainingMaxPending");
        await migrator.MigrateAsync(ours);

        afterDown.Should().BeFalse();
        pendingAfterDown.Should().BeFalse();
        (await ColumnExistsAsync("Exercises", "SupersetGroup")).Should().BeTrue();
        (await ColumnExistsAsync("Exercises", "ExtraSetSide")).Should().BeTrue();
        (await ColumnExistsAsync("ExerciseProgressions", "IsTrainingMaxPending")).Should().BeTrue();
    }

    private async Task<bool> ColumnExistsAsync(string table, string column)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{column}')";
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
