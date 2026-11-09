using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// Meal and shopping-list persistence against the real EF model (jsonb lists via JsonColumn) on a
/// fresh database. Until the integrator adds the AddMeals migration these run on <c>EnsureCreated</c>.
/// </summary>
[Collection("Database")]
public class MealRepositoryTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public MealRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"meals_{Guid.NewGuid():N}"
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

    private static Meal Orzo(UserId user) => Meal.Create(
        user,
        "Creamy Lemon Orzo",
        MealType.Dinner,
        "Light and lemony.",
        new[] { new Ingredient("orzo", 200, "g"), new Ingredient("garlic", 2, "clove"), Ingredient.Unquantified("salt") },
        new[] { "Sear.", "Simmer." },
        new[] { "chicken", "pasta" },
        cutFriendly: true,
        externalKey: "cooks:creamy-lemon-orzo");

    [Fact]
    public async Task Meal_RoundTrips_IncludingJsonLists()
    {
        var repo = new MealRepository(_db);
        var orzo = Orzo(Joe);

        await repo.AddAsync(orzo);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var loaded = await new MealRepository(verify).GetByIdAsync(orzo.Id);
        loaded.Should().NotBeNull();
        loaded!.Should().BeEquivalentTo(orzo, o => o.Excluding(m => m.DomainEvents));
        loaded.Ingredients.Should().Equal(orzo.Ingredients);
        loaded.Steps.Should().Equal("Sear.", "Simmer.");
        loaded.Tags.Should().Equal("chicken", "pasta");
    }

    [Fact]
    public async Task Meal_Update_ReplacesTheJsonLists()
    {
        var repo = new MealRepository(_db);
        var orzo = Orzo(Joe);
        await repo.AddAsync(orzo);
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var editRepo = new MealRepository(edit);
            var meal = (await editRepo.GetByIdAsync(orzo.Id))!;
            meal.Update("Orzo v2", MealType.Lunch, null, new[] { new Ingredient("orzo", 150, "g") }, new[] { "Boil." }, Array.Empty<string>(), false);
            editRepo.Update(meal);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        var reloaded = (await new MealRepository(verify).GetByIdAsync(orzo.Id))!;
        reloaded.Title.Should().Be("Orzo v2");
        reloaded.Type.Should().Be(MealType.Lunch);
        reloaded.Ingredients.Should().Equal(new Ingredient("orzo", 150, "g"));
        reloaded.Steps.Should().Equal("Boil.");
        reloaded.Tags.Should().BeEmpty();
        reloaded.CutFriendly.Should().BeFalse();
    }

    [Fact]
    public async Task ListForUser_OrdersByTypeThenTitle_AndGetMany_SkipsOtherUsers()
    {
        var repo = new MealRepository(_db);
        var dinner = Orzo(Joe);
        var oats = Meal.Create(Joe, "Overnight Oats", MealType.Breakfast, null, new[] { new Ingredient("oats", 80, "g") }, null, null, true);
        var bagel = Meal.Create(Joe, "Bacon Bagel", MealType.Breakfast, null, new[] { new Ingredient("bagel", 2, "piece") }, null, null, true);
        var theirs = Orzo(Other);
        foreach (var m in new[] { dinner, oats, bagel, theirs }) await repo.AddAsync(m);
        await _db.SaveChangesAsync();

        await using var verify = Reopen();
        var verifyRepo = new MealRepository(verify);
        var listed = await verifyRepo.ListForUserAsync(Joe);
        var many = await verifyRepo.GetManyAsync(Joe, new[] { dinner.Id, theirs.Id, new MealId(Guid.NewGuid()) });

        listed.Select(m => m.Title).Should().Equal("Bacon Bagel", "Overnight Oats", "Creamy Lemon Orzo");
        many.Select(m => m.Id).Should().Equal(dinner.Id);
    }

    [Fact]
    public async Task ExternalKey_IsUniquePerUser()
    {
        var repo = new MealRepository(_db);
        await repo.AddAsync(Orzo(Joe));
        await repo.AddAsync(Orzo(Other));
        await _db.SaveChangesAsync();

        await using var dup = Reopen();
        await new MealRepository(dup).AddAsync(Orzo(Joe));
        var act = () => dup.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ShoppingList_RoundTrips_AndTogglePersists()
    {
        var meals = new MealRepository(_db);
        var lists = new ShoppingListRepository(_db);
        var orzo = Orzo(Joe);
        await meals.AddAsync(orzo);
        var list = ShoppingList.Create(Joe, Monday, new[] { orzo.Id }, new[]
        {
            new ShoppingListItem("garlic", 2, "clove", false, new[] { orzo.Id }),
            new ShoppingListItem("salt", null, null, false, new[] { orzo.Id }),
        });
        await lists.AddAsync(list);
        await _db.SaveChangesAsync();

        await using (var edit = Reopen())
        {
            var editRepo = new ShoppingListRepository(edit);
            var loaded = (await editRepo.GetForWeekAsync(Joe, Monday))!;
            loaded.Toggle("garlic", "clove").Should().BeTrue();
            editRepo.Update(loaded);
            await edit.SaveChangesAsync();
        }

        await using var verify = Reopen();
        var reloaded = (await new ShoppingListRepository(verify).GetForWeekAsync(Joe, Monday))!;
        reloaded.Should().BeEquivalentTo(list, o => o.Excluding(l => l.DomainEvents).Excluding(l => l.Items).Excluding(l => l.CheckedCount));
        reloaded.SourceMealIds.Should().Equal(orzo.Id);
        reloaded.Items.Select(i => (i.Name, i.Quantity, i.Unit, i.Checked)).Should().Equal(
            ("garlic", 2m, "clove", true),
            ("salt", (decimal?)null, (string?)null, false));
        reloaded.Items[0].FromMeals.Should().Equal(orzo.Id);
        (await new ShoppingListRepository(verify).GetForWeekAsync(Joe, Monday.AddDays(7))).Should().BeNull();
        (await new ShoppingListRepository(verify).GetForWeekAsync(Other, Monday)).Should().BeNull();
    }

    [Fact]
    public async Task ShoppingList_OnePerUserPerWeek()
    {
        var lists = new ShoppingListRepository(_db);
        await lists.AddAsync(ShoppingList.Create(Joe, Monday, Array.Empty<MealId>()));
        await lists.AddAsync(ShoppingList.Create(Other, Monday, Array.Empty<MealId>()));
        await _db.SaveChangesAsync();

        await using var dup = Reopen();
        await new ShoppingListRepository(dup).AddAsync(ShoppingList.Create(Joe, Monday, Array.Empty<MealId>()));
        var act = () => dup.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
