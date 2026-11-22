using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>A counter habit's step survives the database; <c>AddHabitStep</c> backfills 1 and rolls back cleanly.</summary>
[Collection("Database")]
public class HabitStepPersistenceTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_habit_step_repo_01");

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public HabitStepPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "habitstep");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task RoundTrip_KeepsTheStep()
    {
        var protein = HabitDefinition.Create(Owner, "Protein", HabitType.Counter, 180, 140, "g", 0, icon: "meat", step: 25);
        await new HabitRepository(_db).AddAsync(protein);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = (await new HabitRepository(fresh).GetByIdAsync(protein.Id))!;

        loaded.Step.Should().Be(25);
        loaded.Icon.Should().Be("meat");
    }

    [Fact]
    public async Task Migration_BackfillsStepOne_AndDownDropsTheColumn()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddHabitStep", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        (await ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name = 'Habits' AND column_name = 'Step'")).Should().Be(0L);

        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Habits" ("Id", "UserId", "Name", "Type", "Target", "Minimum", "Order", "Archived") VALUES
            ('bbbbbbbb-0000-0000-0000-000000000001', 'u', 'Water', 'Counter', 6, 4, 0, false);
            """);

        await migrator.MigrateAsync(ours);

        (await ScalarAsync("""SELECT "Step" FROM "Habits" WHERE "Id" = 'bbbbbbbb-0000-0000-0000-000000000001'""")).Should().Be(1m);
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
