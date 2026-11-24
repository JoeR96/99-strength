using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
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
public class CardioStagePersistenceTests : IAsyncLifetime
{
    private static readonly DateTime FixedDate = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public CardioStagePersistenceTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => _db = await MigratedDatabase.CreateAsync(_fixture, "cardiostage");

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task CardioStageWorkout_RoundTrips()
    {
        var stairs = new ExerciseBuilder().WithDay(DayNumber.Day1).WithOrder(1).AsCardioStage(CardioLane.Hard).Build();
        var walk = new ExerciseBuilder().WithDay(DayNumber.Day2).WithOrder(1).AsCardioStage(CardioLane.Easy).Build();
        var workout = new WorkoutBuilder().WithUserId("user_cardiostage_1").WithVariant(ProgramVariant.FourDay)
            .WithExercise(stairs).WithExercise(walk).Build();

        var repository = new WorkoutRepository(_db);
        await repository.AddAsync(workout);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(workout.Id);

        AssertSame(loaded!.Exercises.Single(e => e.Id == stairs.Id), stairs);
        AssertSame(loaded.Exercises.Single(e => e.Id == walk.Id), walk);
        var loadedWalk = (CardioStageStrategy)loaded.Exercises.Single(e => e.Id == walk.Id).Progression;
        loadedWalk.Lane.Should().Be(CardioLane.Easy);
        loadedWalk.RequiresSpeed.Should().BeTrue();
        loadedWalk.WarmUp.Should().BeNull();
        loadedWalk.CoolDown.Should().BeNull();
    }

    [Fact]
    public async Task CardioStageState_ChangedByACompletion_IsPersisted()
    {
        var stairs = new ExerciseBuilder().WithDay(DayNumber.Day1).WithOrder(1).AsCardioStage(CardioLane.Hard).Build();
        var workout = new WorkoutBuilder().WithUserId("user_cardiostage_2").WithVariant(ProgramVariant.FourDay)
            .WithExercise(stairs).Build();
        workout.Start();
        var repository = new WorkoutRepository(_db);
        await repository.AddAsync(workout);
        await _db.SaveChangesAsync();

        workout.CompleteDay(DayNumber.Day1, [Session(stairs)]);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(workout.Id);
        AssertSame(loaded!.Exercises.Single(e => e.Id == stairs.Id), stairs);
    }

    [Fact]
    public async Task CompletedCardioActivity_RoundTrips()
    {
        var stairs = new ExerciseBuilder().WithDay(DayNumber.Day1).WithOrder(1).AsCardioStage(CardioLane.Hard).Build();
        var workout = new WorkoutBuilder().WithUserId("user_cardiostage_3").WithVariant(ProgramVariant.FourDay)
            .WithBlockSequence([1]).WithExercise(stairs)
            .WithExercise(new ExerciseBuilder().WithDay(DayNumber.Day2).WithOrder(1).AsCardioStage(CardioLane.Hard).Build())
            .WithExercise(new ExerciseBuilder().WithDay(DayNumber.Day3).WithOrder(1).AsCardioStage(CardioLane.Hard).Build())
            .WithExercise(new ExerciseBuilder().WithDay(DayNumber.Day4).WithOrder(1).AsCardioStage(CardioLane.Hard).Build())
            .Build();
        workout.Start();
        var planned = stairs.Progression.CalculateCardioBlock()!;
        var repository = new WorkoutRepository(_db);
        await repository.AddAsync(workout);
        await _db.SaveChangesAsync();

        workout.CompleteDay(DayNumber.Day1, [Session(stairs)]);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(workout.Id);
        AssertPerformance(loaded!.CompletedActivities.Single().Performances.Single(), planned);

        CompleteEveryDayUntilFinished(loaded);
        loaded.UpdateBlockSequence([1]);
        loaded.ArchivedActivities.Should().NotBeEmpty();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var archived = await repository.GetByIdAsync(workout.Id);
        archived!.CompletedActivities.Should().BeEmpty();
        AssertPerformance(archived.ArchivedActivities.First().Performances.Single(), planned);
    }

    [Fact]
    public async Task Migration_DownDropsTheCardioStageColumns()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddCardioStageProgression", StringComparison.Ordinal));

        await migrator.MigrateAsync(migrations[migrations.IndexOf(ours) - 1]);
        (await ColumnCountAsync()).Should().Be(0L);

        await migrator.MigrateAsync(ours);
        (await ColumnCountAsync()).Should().Be(6L);
    }

    private async Task<object?> ColumnCountAsync()
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.columns WHERE table_name = 'ExerciseProgressions' AND column_name LIKE 'CardioStage%'";
        return await command.ExecuteScalarAsync();
    }

    private static void CompleteEveryDayUntilFinished(Workout workout)
    {
        while (workout.Status == WorkoutStatus.Active)
        {
            var day = workout.Exercises.Select(e => e.AssignedDay).Distinct().Order().First(d => !workout.IsDayCompletedInCurrentWeek(d));
            workout.CompleteDay(day, [Session(workout.Exercises.Single(e => e.AssignedDay == day))]);
        }
    }

    private static ExercisePerformance Session(Exercise exercise) =>
        new(exercise.Id, [], [], FixedDate,
            plannedCardio: exercise.Progression.CalculateCardioBlock(),
            cardio: new CardioResult(true, EffortRating.Easy, PainFlag.Niggle, "knee", 480, 6m, 6m, 3.5m, 42));

    private static void AssertPerformance(ExercisePerformance performance, PlannedCardioBlock planned)
    {
        performance.PlannedCardio.Should().Be(planned);
        performance.PlannedCardio!.WarmUp.Should().Be(planned.WarmUp);
        performance.PlannedCardio.CoolDown.Should().Be(planned.CoolDown);
        performance.PlannedCardio.Title.Should().Be(planned.Title);
        performance.Cardio.Should().Be(new CardioResult(true, EffortRating.Easy, PainFlag.Niggle, "knee", 480, 6m, 6m, 3.5m, 42));
    }

    private static void AssertSame(Exercise loaded, Exercise original)
    {
        var actual = loaded.Progression.Should().BeOfType<CardioStageStrategy>().Subject;
        var expected = (CardioStageStrategy)original.Progression;
        actual.Lane.Should().Be(expected.Lane);
        actual.RequiresSpeed.Should().Be(expected.RequiresSpeed);
        actual.WarmUp.Should().Be(expected.WarmUp);
        actual.CoolDown.Should().Be(expected.CoolDown);
        actual.Stages.Should().BeEquivalentTo(expected.Stages);
        actual.State.Should().BeEquivalentTo(expected.State);
        loaded.Category.Should().Be(ExerciseCategory.Cardio);
    }
}
