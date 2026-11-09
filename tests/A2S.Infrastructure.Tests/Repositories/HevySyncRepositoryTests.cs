using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// HevyLinks, HevyWorkoutSnapshots and HevyPullCursors on a fresh <c>EnsureCreated</c> database (the
/// integrator scaffolds their migration). Round trips, user scoping and the unique link index.
/// </summary>
[Collection("Database")]
public class HevySyncRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_hevy_repo_0001");
    private static readonly UserId Other = new("user_hevy_repo_0002");
    private static readonly TrainingPlanId Plan = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    private static readonly DateTimeOffset At = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public HevySyncRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await FreshDatabase.CreateAsync(_fixture, "hevy");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Links_RoundTrip_AreScopedToTheUser_AndFoundByKeyOrHevyId()
    {
        var repo = new HevyLinkRepository(_db);
        var liftKey = HevyLocalKeys.LiftRoutine(Plan, Mon16Nov, DayNumber.Day1);
        await repo.AddAsync(HevyLink.Create(Joe, HevyLinkKind.Routine, liftKey, "routine-1", At));
        await repo.AddAsync(HevyLink.Create(Joe, HevyLinkKind.Template, HevyLocalKeys.DrillTemplate("Open books"), "tmpl-1", At));
        await repo.AddAsync(HevyLink.Create(Other, HevyLinkKind.Routine, liftKey, "routine-theirs", At));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var freshRepo = new HevyLinkRepository(fresh);

        var byKey = await freshRepo.FindAsync(Joe, HevyLinkKind.Routine, liftKey);
        var byId = await freshRepo.FindByHevyIdAsync(Joe, HevyLinkKind.Routine, "routine-1");
        var routines = await freshRepo.ListAsync(Joe, HevyLinkKind.Routine);
        var templates = await freshRepo.ListAsync(Joe, HevyLinkKind.Template);

        byKey!.HevyId.Should().Be("routine-1");
        byKey.SyncedAtUtc.Should().Be(At);
        byId!.LocalKey.Should().Be(liftKey);
        routines.Should().ContainSingle().Which.HevyId.Should().Be("routine-1");
        templates.Should().ContainSingle().Which.LocalKey.Should().Be("drill:open-books");
        (await freshRepo.FindAsync(Other, HevyLinkKind.Routine, liftKey))!.HevyId.Should().Be("routine-theirs");
    }

    [Fact]
    public async Task Links_RejectADuplicateKeyForTheSameUserAndKind()
    {
        var repo = new HevyLinkRepository(_db);
        await repo.AddAsync(HevyLink.Create(Joe, HevyLinkKind.Folder, HevyLocalKeys.Folder(Plan), "7", At));
        await _db.SaveChangesAsync();

        await using var second = MigratedDatabase.Reopen(_db);
        await new HevyLinkRepository(second).AddAsync(HevyLink.Create(Joe, HevyLinkKind.Folder, HevyLocalKeys.Folder(Plan), "8", At));
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Snapshots_RoundTrip_AndListByStatus()
    {
        var repo = new HevyWorkoutSnapshotRepository(_db);
        var matched = HevyWorkoutSnapshot.Create(new HevyWorkoutId("w-matched"), Joe, "Upper A", At, At.AddHours(1), "routine-1", "{\"id\":\"w-matched\"}", Mon16Nov, At);
        matched.MarkMatched(new SessionCompletionId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")), At.AddHours(1));
        var unmatched = HevyWorkoutSnapshot.Create(new HevyWorkoutId("w-unmatched"), Joe, "Random", At.AddDays(1), At.AddDays(1).AddMinutes(30), null, "{}", Mon16Nov.AddDays(1), At);
        unmatched.MarkUnmatched("rest day", At.AddDays(1));
        var pending = HevyWorkoutSnapshot.Create(new HevyWorkoutId("w-pending"), Joe, "Lower A", At.AddDays(2), At.AddDays(2).AddHours(1), null, "{}", Mon16Nov.AddDays(2), At);
        var theirs = HevyWorkoutSnapshot.Create(new HevyWorkoutId("w-theirs"), Other, "Theirs", At, At.AddHours(1), null, "{}", Mon16Nov, At);
        theirs.MarkUnmatched("rest day", At);
        foreach (var s in new[] { matched, unmatched, pending, theirs })
        {
            await repo.AddAsync(s);
        }
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var freshRepo = new HevyWorkoutSnapshotRepository(fresh);

        var loaded = await freshRepo.GetByIdAsync(new HevyWorkoutId("w-matched"));
        loaded!.Status.Should().Be(SnapshotStatus.Matched);
        loaded.SessionCompletionId.Should().Be(new SessionCompletionId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")));
        loaded.RoutineId.Should().Be("routine-1");
        loaded.RawJson.Should().Contain("w-matched");
        loaded.LocalDate.Should().Be(Mon16Nov);
        loaded.StartUtc.Should().Be(At);

        var inbox = await freshRepo.ListAsync(Joe, SnapshotStatus.Unmatched);
        inbox.Should().ContainSingle().Which.Reason.Should().Be("rest day");

        var awaiting = await freshRepo.ListAwaitingMatchAsync(Joe);
        awaiting.Select(s => s.Id.Value).Should().Equal("w-unmatched", "w-pending");
    }

    [Fact]
    public async Task Cursor_RoundTrips_AndUpdates()
    {
        var repo = new HevyPullCursorRepository(_db);
        await repo.AddAsync(HevyPullCursor.Start(Joe, At, At));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var freshRepo = new HevyPullCursorRepository(fresh);
        var cursor = await freshRepo.GetByIdAsync(Joe);
        cursor!.LastEventAtUtc.Should().Be(At);

        cursor.Advance(At.AddHours(3), At.AddHours(4));
        freshRepo.Update(cursor);
        await fresh.SaveChangesAsync();

        await using var verify = MigratedDatabase.Reopen(_db);
        var again = await new HevyPullCursorRepository(verify).GetByIdAsync(Joe);
        again!.LastEventAtUtc.Should().Be(At.AddHours(3));
        again.LastPulledAtUtc.Should().Be(At.AddHours(4));
        (await new HevyPullCursorRepository(verify).GetByIdAsync(Other)).Should().BeNull();
    }
}
