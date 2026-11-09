using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.SeedData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace A2S.Infrastructure.Tests.SeedData;

/// <summary>
/// The per-user meal seeder against a fresh database. Until the integrator adds the AddMeals
/// migration these run on <c>EnsureCreated</c> rather than <see cref="MigratedDatabase"/>.
/// </summary>
[Collection("Database")]
public class MealSeederTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");

    private readonly DatabaseFixture _fixture;
    private A2SDbContextHolder _db = null!;

    public MealSeederTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync() => _db = new A2SDbContextHolder(await FreshDatabase.CreateAsync(_fixture, "mealseed"));

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task EnsureForUser_OnAFreshDatabase_InsertsTenMeals_ThenNothing()
    {
        var first = await new MealSeeder(_db.Context, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Joe);

        await using var again = _db.Reopen();
        var second = await new MealSeeder(again, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Joe);

        first.Should().Be(10);
        second.Should().Be(0);
        await using var verify = _db.Reopen();
        (await verify.Meals.CountAsync(m => m.UserId == Joe)).Should().Be(10);
        (await verify.Meals.CountAsync(m => m.UserId == Other)).Should().Be(0);
    }

    [Fact]
    public async Task EnsureForUser_IsPerUser_AndLeavesEditedMealsAlone()
    {
        await new MealSeeder(_db.Context, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Joe);
        await using (var edit = _db.Reopen())
        {
            var orzo = await edit.Meals.SingleAsync(m => m.UserId == Joe && m.ExternalKey!.Contains("orzo"));
            orzo.Update("My orzo", orzo.Type, orzo.Description, orzo.Ingredients, orzo.Steps, orzo.Tags, orzo.CutFriendly);
            await edit.SaveChangesAsync();
        }

        await using var other = _db.Reopen();
        var forOther = await new MealSeeder(other, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Other);
        var forJoeAgain = await new MealSeeder(other, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Joe);

        forOther.Should().Be(10);
        forJoeAgain.Should().Be(0);
        await using var verify = _db.Reopen();
        (await verify.Meals.SingleAsync(m => m.UserId == Joe && m.ExternalKey!.Contains("orzo"))).Title.Should().Be("My orzo");
    }

    [Fact]
    public async Task SeededMeals_RoundTripTheirIngredientsStepsAndTags()
    {
        await new MealSeeder(_db.Context, NullLogger<MealSeeder>.Instance).EnsureForUserAsync(Joe);
        var expected = (await MealSeeder.LoadSeedAsync()).Single(e => e.Key.Contains("barbacoa"));

        await using var verify = _db.Reopen();
        var barbacoa = await verify.Meals.SingleAsync(m => m.UserId == Joe && m.ExternalKey == expected.Key);

        barbacoa.Type.Should().Be(MealType.Dinner);
        barbacoa.Ingredients.Should().HaveCount(expected.Ingredients.Count);
        barbacoa.Ingredients.Single(i => i.Name == "beef chuck").Should().Be(new Ingredient("beef chuck", 1.2m, "kg"));
        barbacoa.Ingredients.Single(i => i.Name == "garlic").Quantity.Should().Be(4);
        barbacoa.Steps.Should().Equal(expected.Steps);
        barbacoa.Tags.Should().Equal(expected.Tags);
    }
}

/// <summary>Owns a fresh database's first context and reopens it with a clean change tracker.</summary>
internal sealed class A2SDbContextHolder : IAsyncDisposable
{
    public A2SDbContextHolder(A2SDbContext context) => Context = context;

    public A2SDbContext Context { get; }

    public A2SDbContext Reopen() => MigratedDatabase.Reopen(Context);

    public async ValueTask DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}
