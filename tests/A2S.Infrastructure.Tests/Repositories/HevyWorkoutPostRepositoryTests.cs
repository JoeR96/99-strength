using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Gym mode's persistence on the real migrated schema: the HevyWorkoutPosts table (round trip, the
/// unique client-session index, the due list), the migration's Down, and a lift slot's warm-up and
/// finisher surviving the schedule's jsonb column.
/// </summary>
[Collection("Database")]
public class HevyWorkoutPostRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_gym_repo_0001");
    private static readonly UserId Other = new("user_gym_repo_0002");
    private static readonly Guid ClientSession = Guid.Parse("9a9a9a9a-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 11, 16, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public HevyWorkoutPostRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "gympost");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static HevyWorkoutPost Post(UserId user, Guid clientSession, DateOnly date) =>
        HevyWorkoutPost.Create(user, clientSession, null, null, date, "Upper A", Start, End, "{\"sets\":[]}", "{\"ok\":true}", End);

    [Fact]
    public async Task Posts_RoundTrip_AndAreFoundByClientSession()
    {
        var post = Post(Joe, ClientSession, Mon16Nov);
        post.RecordFailure("Hevy unavailable", permanent: false, End);
        var repo = new HevyWorkoutPostRepository(_db);
        await repo.AddAsync(post);
        await _db.SaveChangesAsync();

        await using var reopened = MigratedDatabase.Reopen(_db);
        var again = new HevyWorkoutPostRepository(reopened);
        var found = await again.FindByClientSessionAsync(Joe, ClientSession);
        var otherUser = await again.FindByClientSessionAsync(Other, ClientSession);

        found.Should().NotBeNull();
        found!.Title.Should().Be("Upper A");
        found.Attempts.Should().Be(1);
        found.Status.Should().Be(HevyPostStatus.Pending);
        found.NextAttemptAtUtc.Should().Be(End.AddMinutes(2));
        found.PayloadJson.Should().Contain("sets");
        otherUser.Should().BeNull();
    }

    [Fact]
    public async Task ClientSession_IsUniquePerUser()
    {
        _db.HevyWorkoutPosts.Add(Post(Joe, ClientSession, Mon16Nov));
        _db.HevyWorkoutPosts.Add(Post(Joe, ClientSession, Mon16Nov));

        var act = () => _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ListDue_ReturnsOnlyPendingPostsWhoseTimeHasCome()
    {
        var due = Post(Joe, ClientSession, Mon16Nov);
        var later = Post(Joe, Guid.Parse("9a9a9a9a-0000-0000-0000-000000000002"), Mon16Nov);
        later.RecordFailure("down", permanent: false, End);
        var posted = Post(Other, Guid.Parse("9a9a9a9a-0000-0000-0000-000000000003"), Mon16Nov);
        posted.MarkPosted("hevy-1", End);
        _db.HevyWorkoutPosts.AddRange(due, later, posted);
        await _db.SaveChangesAsync();

        var list = await new HevyWorkoutPostRepository(_db).ListDueAsync(End.AddMinutes(1), 10);
        var forJoe = await new HevyWorkoutPostRepository(_db).ListForUserAsync(Joe, Mon16Nov, Mon16Nov);

        list.Select(p => p.Id).Should().Equal(due.Id);
        forJoe.Should().HaveCount(2);
    }

    [Fact]
    public async Task LiftWarmUpAndFinisher_SurviveTheScheduleColumn()
    {
        var warmUp = new MobilityRoutineId(Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001"));
        var stairs = new CardioProtocolId(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000003"));
        var plan = TrainingPlan.Create(Joe, "Cut", Mon16Nov,
            [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", warmUp, new SessionFinisher(stairs)),
             ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A")], 95m);
        _db.TrainingPlans.Add(plan);
        await _db.SaveChangesAsync();

        await using var reopened = MigratedDatabase.Reopen(_db);
        var loaded = await reopened.TrainingPlans.SingleAsync(p => p.Id == plan.Id);

        loaded.Schedule[0].WarmUpRoutineId.Should().Be(warmUp);
        loaded.Schedule[0].Finisher.Should().Be(new SessionFinisher(stairs));
        loaded.Schedule[1].WarmUpRoutineId.Should().BeNull();
        loaded.Schedule[1].Finisher.Should().BeNull();
    }

    [Fact]
    public async Task Migration_IsReversible()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_AddHevyWorkoutPosts", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];

        await migrator.MigrateAsync(previous);
        var afterDown = await TableExistsAsync();
        await migrator.MigrateAsync(ours);
        var afterUp = await TableExistsAsync();

        afterDown.Should().BeFalse();
        afterUp.Should().BeTrue();
    }

    private async Task<bool> TableExistsAsync()
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"HevyWorkoutPosts\"') IS NOT NULL";
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
