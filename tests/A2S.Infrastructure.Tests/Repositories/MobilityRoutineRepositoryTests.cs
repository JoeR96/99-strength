using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

[Collection("Database")]
public class MobilityRoutineRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_mobility_repo_0001");
    private static readonly UserId OtherUser = new("user_mobility_repo_0002");

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public MobilityRoutineRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await FreshDatabase.CreateAsync(_fixture, "mobility");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static MobilityRoutine MobilityA(UserId owner) => MobilityRoutine.Create(owner, "Mobility A", 10,
    [
        new MobilityDrill("90/90 breathing", 2, DrillDose.Breaths(5), SideBias.None, "Ribs down", "Back-to-wall breathing", false),
        new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, "Side-on wall rotation", true),
        new MobilityDrill("Wall slides with lift-off", 2, DrillDose.Reps(10), SideBias.None, null, null, false)
    ]);

    [Fact]
    public async Task RoundTrip_ReloadsDrillsInOrderWithDosesAndAlternatives()
    {
        var routine = MobilityA(Owner);
        await new MobilityRoutineRepository(_db).AddAsync(routine);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new MobilityRoutineRepository(fresh).GetByIdAsync(routine.Id);

        loaded!.Name.Should().Be("Mobility A");
        loaded.TargetMinutes.Should().Be(10);
        loaded.UserId.Should().Be(Owner);
        loaded.Drills.Should().Equal(routine.Drills);
        loaded.Drills[1].Dose.Should().Be(DrillDose.Reps(5));
        loaded.Drills[1].ExtraSetSide.Should().Be(SideBias.Left);
        loaded.Drills[1].PerSide.Should().BeTrue();
        loaded.Drills[0].StandingAlternative.Should().Be("Back-to-wall breathing");
    }

    [Fact]
    public async Task Update_AfterReorderingAndReplacing_PersistsTheNewOrder()
    {
        var routine = MobilityA(Owner);
        var repository = new MobilityRoutineRepository(_db);
        await repository.AddAsync(routine);
        await _db.SaveChangesAsync();

        routine.MoveDrill("Wall slides with lift-off", 0);
        routine.AddDrill(new MobilityDrill("Bird dog", 3, DrillDose.Reps(5), SideBias.Right, null, null, true));
        repository.Update(routine);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new MobilityRoutineRepository(fresh).GetByIdAsync(routine.Id);

        loaded!.Drills.Select(d => d.Name).Should().Equal("Wall slides with lift-off", "90/90 breathing", "Open books", "Bird dog");
    }

    [Fact]
    public async Task ListForUserAsync_IsScopedToTheUser()
    {
        var repository = new MobilityRoutineRepository(_db);
        await repository.AddAsync(MobilityA(Owner));
        await repository.AddAsync(MobilityA(OtherUser));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var mine = await new MobilityRoutineRepository(fresh).ListForUserAsync(Owner);

        mine.Should().ContainSingle().Which.UserId.Should().Be(Owner);
    }
}
