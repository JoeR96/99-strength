using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

[Collection("Database")]
public class CardioProtocolRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_cardio_repo_0001");
    private static readonly UserId OtherUser = new("user_cardio_repo_0002");

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public CardioProtocolRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await FreshDatabase.CreateAsync(_fixture, "cardio");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task RoundTrip_ReloadsIntervalsAndWeekState()
    {
        var repository = new CardioProtocolRepository(_db);
        var fourByFour = CardioProtocol.NorwegianFourByFour(Owner);
        var c25k = CardioProtocol.CouchToFiveK(Owner, 3, repeatWeek: true);
        var liss = CardioProtocol.Liss(Owner, 35, "Incline walk");
        await repository.AddAsync(fourByFour);
        await repository.AddAsync(c25k);
        await repository.AddAsync(liss);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var freshRepository = new CardioProtocolRepository(fresh);
        var loadedFourByFour = await freshRepository.GetByIdAsync(fourByFour.Id);
        var loadedC25k = await freshRepository.GetByIdAsync(c25k.Id);
        var loadedLiss = await freshRepository.GetByIdAsync(liss.Id);

        loadedFourByFour!.Intervals.Should().Equal(fourByFour.Intervals);
        loadedFourByFour.TotalMinutes.Should().Be(40);
        loadedFourByFour.Kind.Should().Be(CardioProtocolKind.NorwegianFourByFour);
        loadedC25k!.WeekIndex.Should().Be(3);
        loadedC25k.RepeatWeek.Should().BeTrue();
        loadedC25k.Intervals.Should().Equal(c25k.Intervals);
        loadedLiss!.Notes.Should().Be("Incline walk");
        loadedLiss.Intervals.Should().ContainSingle().Which.Effort.Should().Be(CardioEffort.Easy);
    }

    [Fact]
    public async Task Update_AfterNextWeek_PersistsTheNewIntervals()
    {
        var repository = new CardioProtocolRepository(_db);
        var c25k = CardioProtocol.CouchToFiveK(Owner, 8);
        await repository.AddAsync(c25k);
        await _db.SaveChangesAsync();

        c25k.NextWeek();
        repository.Update(c25k);
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var loaded = await new CardioProtocolRepository(fresh).GetByIdAsync(c25k.Id);

        loaded!.WeekIndex.Should().Be(9);
        loaded.Name.Should().Be("Couch to 5K — Week 9");
        loaded.Intervals.Should().Equal(CardioProtocol.CouchToFiveK(Owner, 9).Intervals);
    }

    [Fact]
    public async Task ListForUserAsync_IsScopedToTheUser()
    {
        var repository = new CardioProtocolRepository(_db);
        await repository.AddAsync(CardioProtocol.NorwegianFourByFour(Owner));
        await repository.AddAsync(CardioProtocol.Liss(Owner, 30, null));
        await repository.AddAsync(CardioProtocol.NorwegianFourByFour(OtherUser));
        await _db.SaveChangesAsync();

        await using var fresh = MigratedDatabase.Reopen(_db);
        var mine = await new CardioProtocolRepository(fresh).ListForUserAsync(Owner);

        mine.Should().HaveCount(2);
        mine.Should().AllSatisfy(p => p.UserId.Should().Be(Owner));
    }
}
