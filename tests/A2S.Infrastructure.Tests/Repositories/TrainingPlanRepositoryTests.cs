using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Round-trips the TrainingPlan aggregate through a fresh EnsureCreated database (no migration yet):
/// JSON-owned schedule, phases, checkpoints and allowance uses, plus the SessionCompletions child table.
/// </summary>
[Collection("Database")]
public class TrainingPlanRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_plan_repo_0001");
    private static readonly UserId OtherUser = new("user_plan_repo_0002");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-000000000001"));
    private static readonly WorkoutId LinkedWorkout = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset RecordedAt = new(2026, 11, 16, 18, 30, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public TrainingPlanRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await FreshDatabase.CreateAsync(_fixture, "plan");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static TrainingPlan FullPlan(UserId owner)
    {
        var plan = TrainingPlan.Create(owner, "Cut to 85 — Nov 2026", new DateOnly(2026, 11, 11),
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
            ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
            ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K or LISS"),
            ScheduledSession.Rest(DayOfWeek.Sunday, "Rest — walk only")
        ], 95m, new WeeklyAllowances(1, 1));

        plan.AddPhase(new PlanPhase(PhaseType.Cut, Mon16Nov, new DateOnly(2026, 12, 21), null));
        plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3), "Christmas"));
        plan.AddCheckpoint(new Checkpoint(new DateOnly(2026, 12, 7), 92m, "4 cutting weeks"));
        plan.AddCheckpoint(new Checkpoint(new DateOnly(2027, 3, 8), 85m, "Goal"));
        plan.LinkWorkout(LinkedWorkout);
        plan.UseAllowance(new DateOnly(2026, 11, 20), AllowanceKind.FlexibleMeal);
        plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Hevy,
            liftDay: DayNumber.Day1, hevyWorkoutId: "hevy-123", recordedAt: RecordedAt);
        plan.CompleteSession(new DateOnly(2026, 11, 18), SessionKind.Mobility, CompletionSource.Manual,
            mobility: MobilityB, drillTicks: [true, true, false, true, true], recordedAt: RecordedAt.AddDays(2));
        plan.CompleteSession(new DateOnly(2026, 11, 18), SessionKind.Cardio, CompletionSource.Manual,
            cardio: FourByFour, log: new CardioLog(2580, 165, 8, 6100m, "Hills"), recordedAt: RecordedAt.AddDays(2));
        return plan;
    }

    [Fact]
    public async Task RoundTrip_ReloadsScheduleePhasesCheckpointsAllowancesAndCompletions()
    {
        var plan = FullPlan(Owner);
        await new TrainingPlanRepository(_db).AddAsync(plan);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new TrainingPlanRepository(fresh).GetByIdAsync(plan.Id);

        loaded.Should().NotBeNull();
        loaded!.UserId.Should().Be(Owner);
        loaded.Name.Should().Be("Cut to 85 — Nov 2026");
        loaded.StartDate.Should().Be(Mon16Nov);
        loaded.IsActive.Should().BeTrue();
        loaded.LinkedWorkoutId.Should().Be(LinkedWorkout);
        loaded.StartWeightKg.Should().Be(95m);
        loaded.Allowances.Should().Be(new WeeklyAllowances(1, 1));

        loaded.Schedule.Should().Equal(plan.Schedule);
        loaded.Phases.Should().Equal(plan.Phases);
        loaded.Checkpoints.Should().Equal(plan.Checkpoints);
        loaded.AllowanceUses.Should().Equal(plan.AllowanceUses);

        loaded.Completions.Should().HaveCount(3);
        var lift = loaded.Completions.Single(c => c.Kind == SessionKind.Lift);
        lift.Id.Should().Be(plan.Completions.Single(c => c.Kind == SessionKind.Lift).Id);
        lift.Date.Should().Be(Mon16Nov);
        lift.LiftDay.Should().Be(DayNumber.Day1);
        lift.Source.Should().Be(CompletionSource.Hevy);
        lift.HevyWorkoutId.Should().Be("hevy-123");
        lift.RecordedAtUtc.Should().Be(RecordedAt);
        lift.Cardio.Should().BeNull();
        lift.DrillTicks.Should().BeNull();

        var mobility = loaded.Completions.Single(c => c.Kind == SessionKind.Mobility);
        mobility.MobilityRoutineId.Should().Be(MobilityB);
        mobility.DrillTicks.Should().Equal(true, true, false, true, true);

        var cardio = loaded.Completions.Single(c => c.Kind == SessionKind.Cardio);
        cardio.CardioProtocolId.Should().Be(FourByFour);
        cardio.Cardio.Should().Be(new CardioLog(2580, 165, 8, 6100m, "Hills"));
    }

    [Fact]
    public async Task Update_AfterUndoAndNewCompletion_PersistsTheChange()
    {
        var plan = FullPlan(Owner);
        var repository = new TrainingPlanRepository(_db);
        await repository.AddAsync(plan);
        await _db.SaveChangesAsync();

        var lift = plan.Completions.Single(c => c.Kind == SessionKind.Lift);
        plan.UndoCompletion(lift.Id).Should().BeTrue();
        plan.CompleteSession(new DateOnly(2026, 11, 17), SessionKind.Lift, CompletionSource.Manual,
            liftDay: DayNumber.Day2, recordedAt: RecordedAt.AddDays(1));
        plan.RemovePhase(new DateOnly(2026, 12, 22)).Should().BeTrue();
        repository.Update(plan);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new TrainingPlanRepository(fresh).GetByIdAsync(plan.Id);

        loaded!.Completions.Should().HaveCount(3);
        loaded.Completions.Should().NotContain(c => c.Id == lift.Id);
        loaded.Completions.Should().ContainSingle(c => c.Kind == SessionKind.Lift && c.LiftDay == DayNumber.Day2);
        loaded.Phases.Should().ContainSingle().Which.Type.Should().Be(PhaseType.Cut);
        (await fresh.Set<SessionCompletion>().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task GetActiveForUserAsync_ReturnsOnlyThatUsersActivePlan()
    {
        var repository = new TrainingPlanRepository(_db);
        var inactive = FullPlan(Owner);
        inactive.Deactivate();
        var active = FullPlan(Owner);
        var someoneElses = FullPlan(OtherUser);
        await repository.AddAsync(inactive);
        await repository.AddAsync(active);
        await repository.AddAsync(someoneElses);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var freshRepository = new TrainingPlanRepository(fresh);
        var found = await freshRepository.GetActiveForUserAsync(Owner);
        var none = await freshRepository.GetActiveForUserAsync(new UserId("user_plan_repo_nobody"));

        found!.Id.Should().Be(active.Id);
        found.Completions.Should().HaveCount(3);
        none.Should().BeNull();
    }

    [Fact]
    public async Task ListForUserAsync_IsScopedToTheUser()
    {
        var repository = new TrainingPlanRepository(_db);
        await repository.AddAsync(FullPlan(Owner));
        await repository.AddAsync(FullPlan(Owner));
        await repository.AddAsync(FullPlan(OtherUser));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var mine = await new TrainingPlanRepository(fresh).ListForUserAsync(Owner);

        mine.Should().HaveCount(2);
        mine.Should().AllSatisfy(p => p.UserId.Should().Be(Owner));
    }

    [Fact]
    public async Task Schema_HasTheFilteredUniqueHevyIndexAndTheActiveIndex()
    {
        var indexDefinitions = await _db.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename IN ('SessionCompletions', 'TrainingPlans')")
            .ToListAsync();

        indexDefinitions.Should().Contain(d =>
            d.Contains("UNIQUE") && d.Contains("\"TrainingPlanId\"") && d.Contains("\"HevyWorkoutId\"") && d.Contains("IS NOT NULL"));
        indexDefinitions.Should().Contain(d => d.Contains("\"UserId\"") && d.Contains("\"IsActive\""));
    }
}
