using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Habit and body-weight persistence against the real EF model on a fresh database. Until the integrator
/// adds the migration these run on <c>EnsureCreated</c> rather than <see cref="MigratedDatabase"/>.
/// </summary>
[Collection("Database")]
public class HabitRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public HabitRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"habits_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<A2SDbContext>().UseNpgsql(builder.ConnectionString).Options;
        _db = new A2SDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private A2SDbContext Reopen() => MigratedDatabase.Reopen(_db);

    [Fact]
    public async Task Habit_RoundTrips()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water (532ml bottle)", HabitType.Counter, 6, 4, "bottles", 0, "droplet", "terran:water");

        await repo.AddAsync(water);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var loaded = await new HabitRepository(verify).GetByIdAsync(water.Id);
        loaded.Should().NotBeNull();
        loaded!.Should().BeEquivalentTo(water, o => o.Excluding(h => h.DomainEvents));
    }

    [Fact]
    public async Task ListForUser_OrdersByOrder_AndHidesArchivedUnlessAsked()
    {
        var repo = new HabitRepository(_db);
        var walks = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 1);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        var old = HabitDefinition.Create(Joe, "Old", HabitType.Checkbox, 1, 1, null, 2);
        old.Archive();
        var theirs = HabitDefinition.Create(Other, "Theirs", HabitType.Checkbox, 1, 1, null, 0);
        foreach (var h in new[] { walks, water, old, theirs }) await repo.AddAsync(h);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var active = await new HabitRepository(verify).ListForUserAsync(Joe, includeArchived: false);
        var all = await new HabitRepository(verify).ListForUserAsync(Joe, includeArchived: true);

        active.Select(h => h.Name).Should().Equal("Water", "Walks");
        all.Select(h => h.Name).Should().Equal("Water", "Walks", "Old");
    }

    [Fact]
    public async Task ExternalKey_IsUniquePerUser_ButNullIsNot()
    {
        var repo = new HabitRepository(_db);
        await repo.AddAsync(HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 0, externalKey: "terran:walks"));
        await repo.AddAsync(HabitDefinition.Create(Joe, "A", HabitType.Checkbox, 1, 1, null, 1));
        await repo.AddAsync(HabitDefinition.Create(Joe, "B", HabitType.Checkbox, 1, 1, null, 2));
        await repo.AddAsync(HabitDefinition.Create(Other, "Walks", HabitType.Counter, 3, 1, null, 0, externalKey: "terran:walks"));
        await _db.SaveChangesAsync();

        await using var dup = Reopen();
        await new HabitRepository(dup).AddAsync(HabitDefinition.Create(Joe, "Walks again", HabitType.Counter, 3, 1, null, 3, externalKey: "terran:walks"));
        var act = () => dup.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Logs_RoundTrip_AndRangeQueryIsInclusive()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        var theirs = HabitDefinition.Create(Other, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        await repo.AddAsync(theirs);
        var nov30 = new DateOnly(2026, 11, 30);
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, nov30.AddDays(-1), 6, At));
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, nov30, 4, At));
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, new DateOnly(2026, 12, 2), 7, At));
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, new DateOnly(2026, 12, 3), 1, At));
        await repo.AddLogAsync(HabitLog.Create(theirs.Id, Other, nov30, 6, At));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new HabitRepository(verify);
        var logs = await verifyRepo.GetLogsAsync(Joe, nov30, new DateOnly(2026, 12, 2));
        var single = await verifyRepo.GetLogAsync(water.Id, new DateOnly(2026, 12, 2));
        var missing = await verifyRepo.GetLogAsync(water.Id, new DateOnly(2026, 12, 1));

        logs.Select(l => (l.Date, l.Value)).Should().Equal((nov30, 4m), (new DateOnly(2026, 12, 2), 7m));
        single!.Value.Should().Be(7);
        single.UpdatedAtUtc.Should().Be(At);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Logs_OnePerHabitPerDate()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        var day = new DateOnly(2026, 11, 9);
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, day, 1, At));
        await _db.SaveChangesAsync();

        await using var dup = Reopen();
        await new HabitRepository(dup).AddLogAsync(HabitLog.Create(water.Id, Joe, day, 2, At));
        var act = () => dup.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UpdateLog_PersistsNewValueAndTimestamp()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        var day = new DateOnly(2026, 11, 9);
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, day, 1, At));
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var editRepo = new HabitRepository(edit);
            var log = await editRepo.GetLogAsync(water.Id, day);
            log!.Apply(5, At.AddMinutes(10));
            editRepo.UpdateLog(log);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        var reloaded = await new HabitRepository(verify).GetLogAsync(water.Id, day);
        reloaded!.Value.Should().Be(5);
        reloaded.UpdatedAtUtc.Should().Be(At.AddMinutes(10));
    }

    [Theory]
    [InlineData(1_230_000)]   // .123 s: the milliseconds a browser stamps
    [InlineData(1_234_567)]   // .1234567 s: every 100 ns tick .NET can hold
    [InlineData(9_999_999)]   // .9999999 s: rounding up would land in the next second
    public async Task LogTimestamp_SurvivesPostgresToTheMicrosecond_SoARedeliveredTapIsStillRecognised(long ticksIntoTheSecond)
    {
        var tappedAt = new DateTimeOffset(At.Ticks + ticksIntoTheSecond, TimeSpan.Zero);
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        var day = new DateOnly(2026, 11, 9);
        await repo.AddLogAsync(HabitLog.CreateFromTap(water.Id, Joe, day, HabitType.Counter, 1, tappedAt));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var reloaded = await new HabitRepository(verify).GetLogAsync(water.Id, day);

        // timestamptz keeps microseconds: the seventh digit is cut off, never rounded.
        reloaded!.UpdatedAtUtc.Ticks.Should().Be(tappedAt.Ticks - tappedAt.Ticks % 10);
        reloaded.IsRedeliveryOf(tappedAt).Should().BeTrue();
        reloaded.IsRedeliveryOf(tappedAt.AddMilliseconds(1)).Should().BeFalse();
        reloaded.ApplyTap(HabitType.Counter, 1, tappedAt).Should().BeFalse();
        reloaded.Value.Should().Be(1);
    }

    [Fact]
    public async Task LogWithoutATimestamp_RoundTripsAsTheZeroInstant_AndIsNeverARedelivery()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        var day = new DateOnly(2026, 11, 9);
        await repo.AddLogAsync(HabitLog.CreateFromTap(water.Id, Joe, day, HabitType.Counter, 1, default));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var reloaded = await new HabitRepository(verify).GetLogAsync(water.Id, day);

        reloaded!.UpdatedAtUtc.Should().Be(default(DateTimeOffset));
        reloaded.ApplyTap(HabitType.Counter, 1, default).Should().BeTrue();
        reloaded.Value.Should().Be(2);
    }

    [Fact]
    public async Task RemovingAHabit_CascadesToItsLogs()
    {
        var repo = new HabitRepository(_db);
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        await repo.AddAsync(water);
        await repo.AddLogAsync(HabitLog.Create(water.Id, Joe, new DateOnly(2026, 11, 9), 1, At));
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var editRepo = new HabitRepository(edit);
            editRepo.Remove((await editRepo.GetByIdAsync(water.Id))!);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        (await verify.HabitLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BodyWeight_RoundTrips_ListsInDateOrder_AndIsUniquePerDate()
    {
        var repo = new BodyWeightRepository(_db);
        var day1 = new DateOnly(2026, 11, 9);
        await repo.AddAsync(BodyWeightEntry.Create(Joe, day1.AddDays(1), 94.6m, At));
        await repo.AddAsync(BodyWeightEntry.Create(Joe, day1, 95m, At));
        await repo.AddAsync(BodyWeightEntry.Create(Joe, day1.AddDays(5), 94m, At));
        await repo.AddAsync(BodyWeightEntry.Create(Other, day1, 80m, At));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new BodyWeightRepository(verify);
        var listed = await verifyRepo.ListAsync(Joe, day1, day1.AddDays(1));
        var single = await verifyRepo.GetForDateAsync(Joe, day1);

        listed.Select(e => e.Kg).Should().Equal(95m, 94.6m);
        single!.Kg.Should().Be(95m);
        single.RecordedAtUtc.Should().Be(At);

        await using var dup = Reopen();
        await new BodyWeightRepository(dup).AddAsync(BodyWeightEntry.Create(Joe, day1, 96m, At));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
