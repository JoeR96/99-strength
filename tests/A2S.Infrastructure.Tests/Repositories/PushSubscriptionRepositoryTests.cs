using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Push subscription and nudge-log persistence on a fresh database (<c>EnsureCreated</c>), as the habit
/// tests do until the integrator adds the <c>AddPushSubscriptions</c> migration.
/// </summary>
[Collection("Database")]
public class PushSubscriptionRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 9);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public PushSubscriptionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = $"push_{Guid.NewGuid():N}" };
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
    public async Task Subscription_RoundTrips_AndIsFoundByEndpoint()
    {
        var repo = new PushSubscriptionRepository(_db);
        var sub = PushSubscription.Create(Joe, "https://push.example.com/a", "p256", "auth", "Safari iOS", At, new TimeOnly(6, 30));
        await repo.AddAsync(sub);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new PushSubscriptionRepository(verify);
        var loaded = await verifyRepo.GetByEndpointAsync(Joe, "https://push.example.com/a");
        var theirs = await verifyRepo.GetByEndpointAsync(Other, "https://push.example.com/a");

        loaded.Should().NotBeNull();
        loaded!.Should().BeEquivalentTo(sub, o => o.Excluding(s => s.DomainEvents));
        loaded.SessionStartTime.Should().Be(new TimeOnly(6, 30));
        theirs.Should().BeNull();
    }

    [Fact]
    public async Task OneRowPerUserAndEndpoint()
    {
        var repo = new PushSubscriptionRepository(_db);
        await repo.AddAsync(PushSubscription.Create(Joe, "https://push.example.com/a", "p", "a", null, At));
        await repo.AddAsync(PushSubscription.Create(Other, "https://push.example.com/a", "p", "a", null, At));
        await _db.SaveChangesAsync();

        await using var dup = Reopen();
        await new PushSubscriptionRepository(dup).AddAsync(PushSubscription.Create(Joe, "https://push.example.com/a", "p2", "a2", null, At));
        var act = () => dup.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ListForUser_OrdersByCreation_AndSubscribedUsers_AreDistinct()
    {
        var repo = new PushSubscriptionRepository(_db);
        await repo.AddAsync(PushSubscription.Create(Joe, "https://push.example.com/b", "p", "a", null, At.AddMinutes(5)));
        await repo.AddAsync(PushSubscription.Create(Joe, "https://push.example.com/a", "p", "a", null, At));
        await repo.AddAsync(PushSubscription.Create(Other, "https://push.example.com/c", "p", "a", null, At));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new PushSubscriptionRepository(verify);
        var joes = await verifyRepo.ListForUserAsync(Joe);
        var users = await verifyRepo.ListSubscribedUsersAsync();

        joes.Select(s => s.Endpoint).Should().Equal("https://push.example.com/a", "https://push.example.com/b");
        users.Should().BeEquivalentTo(new[] { Joe, Other });
    }

    [Fact]
    public async Task NudgeLog_IsOnePerUserDateAndKind()
    {
        var repo = new PushSubscriptionRepository(_db);
        await repo.AddNudgeAsync(PushNudgeLog.Create(Joe, Day, PushNudgeKind.SessionStart, At));
        await repo.AddNudgeAsync(PushNudgeLog.Create(Joe, Day, PushNudgeKind.HabitsUnderMinimum, At));
        await repo.AddNudgeAsync(PushNudgeLog.Create(Joe, Day.AddDays(1), PushNudgeKind.SessionStart, At));
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new PushSubscriptionRepository(verify);
        (await verifyRepo.HasNudgeAsync(Joe, Day, PushNudgeKind.SessionStart)).Should().BeTrue();
        (await verifyRepo.HasNudgeAsync(Joe, Day, PushNudgeKind.HabitsUnderMinimum)).Should().BeTrue();
        (await verifyRepo.HasNudgeAsync(Joe, Day.AddDays(2), PushNudgeKind.SessionStart)).Should().BeFalse();
        (await verifyRepo.HasNudgeAsync(Other, Day, PushNudgeKind.SessionStart)).Should().BeFalse();

        await using var dup = Reopen();
        await new PushSubscriptionRepository(dup).AddNudgeAsync(PushNudgeLog.Create(Joe, Day, PushNudgeKind.SessionStart, At.AddHours(1)));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Remove_DeletesTheSubscription()
    {
        var repo = new PushSubscriptionRepository(_db);
        var sub = PushSubscription.Create(Joe, "https://push.example.com/a", "p", "a", null, At);
        await repo.AddAsync(sub);
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var editRepo = new PushSubscriptionRepository(edit);
            editRepo.Remove((await editRepo.GetByEndpointAsync(Joe, sub.Endpoint))!);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        (await verify.PushSubscriptions.CountAsync()).Should().Be(0);
    }
}
