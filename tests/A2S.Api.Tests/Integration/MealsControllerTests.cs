using System.Net;
using System.Net.Http.Json;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Meals and the shopping list over HTTP: seeding on first visit, CRUD, the weekly menu, generate,
/// toggle, regenerate keeping ticks, export, auth. Needs the Meals/ShoppingLists tables (the
/// integrator's AddMeals migration).
/// </summary>
[Collection("Integration")]
public class MealsControllerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private readonly TestWebApplicationFactory<Program> _factory;

    public MealsControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient NewUser() => _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

    private static async Task<IReadOnlyList<MealDto>> ListMeals(HttpClient client) =>
        (await client.GetFromJsonAsync<IReadOnlyList<MealDto>>("/api/v1/meals"))!;

    private static object Orzo() => new
    {
        title = "Garlic Orzo",
        type = "Dinner",
        description = "Quick.",
        ingredients = new object[]
        {
            new { name = "orzo", quantity = 200, unit = "g" },
            new { name = "garlic", quantity = 2, unit = "clove" },
            new { name = "salt" },
        },
        steps = new[] { "Boil.", "Stir." },
        tags = new[] { "pasta" },
        cutFriendly = true,
    };

    [Fact]
    public async Task FirstVisit_SeedsTheTenStarterMeals_SecondVisitAddsNone()
    {
        var client = NewUser();

        var first = await ListMeals(client);
        var second = await ListMeals(client);

        first.Should().HaveCount(10);
        first.Should().OnlyContain(m => m.CutFriendly && m.ExternalKey!.StartsWith("cooks:"));
        first.Select(m => m.Type).Should().Contain(MealType.Breakfast).And.Contain(MealType.Dinner);
        second.Select(m => m.Id).Should().BeEquivalentTo(first.Select(m => m.Id));
    }

    [Fact]
    public async Task Create_Update_Delete_RoundTrip()
    {
        var client = NewUser();

        var created = await client.PostAsJsonAsync("/api/v1/meals", Orzo());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var meal = (await created.Content.ReadFromJsonAsync<MealDto>())!;
        meal.Title.Should().Be("Garlic Orzo");
        meal.Type.Should().Be(MealType.Dinner);
        meal.Ingredients.Should().Equal(new IngredientDto("orzo", 200, "g"), new IngredientDto("garlic", 2, "clove"), new IngredientDto("salt", null, null));

        var updated = await client.PutAsJsonAsync($"/api/v1/meals/{meal.Id}", new
        {
            title = "Garlic Orzo v2", type = "Lunch", ingredients = new object[] { new { name = "orzo", quantity = 150, unit = "g" } }, cutFriendly = false,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var v2 = (await updated.Content.ReadFromJsonAsync<MealDto>())!;
        v2.Title.Should().Be("Garlic Orzo v2");
        v2.Type.Should().Be(MealType.Lunch);
        v2.Ingredients.Should().ContainSingle();

        var deleted = await client.DeleteAsync($"/api/v1/meals/{meal.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListMeals(client)).Should().NotContain(m => m.Id == meal.Id);
        (await client.DeleteAsync($"/api/v1/meals/{meal.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownUnit_IsRejected()
    {
        var client = NewUser();

        var response = await client.PostAsJsonAsync("/api/v1/meals", new
        {
            title = "Bad", type = "Snack", ingredients = new object[] { new { name = "flour", quantity = 1, unit = "cup" } },
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task WeekMenu_Generate_Toggle_Regenerate_KeepsTicks_AndDropsRemovedMeal()
    {
        var client = NewUser();
        var meals = await ListMeals(client);
        var orzo = meals.Single(m => m.ExternalKey!.Contains("orzo"));
        var barbacoa = meals.Single(m => m.ExternalKey!.Contains("barbacoa"));

        var set = await client.PutAsJsonAsync("/api/v1/meals/week", new { weekStart = Monday.AddDays(2), mealIds = new[] { orzo.Id, barbacoa.Id } });
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var week = (await set.Content.ReadFromJsonAsync<WeekMealsDto>())!;
        week.WeekStart.Should().Be(Monday);
        week.MealIds.Should().Equal(orzo.Id, barbacoa.Id);
        (await client.GetFromJsonAsync<WeekMealsDto>($"/api/v1/meals/week?week={Monday:yyyy-MM-dd}"))!.MealIds.Should().Equal(orzo.Id, barbacoa.Id);

        var generated = await client.PostAsJsonAsync("/api/v1/shopping-list/generate", new { weekStart = Monday });
        generated.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await generated.Content.ReadFromJsonAsync<ShoppingListDto>())!;
        list.Id.Should().NotBeNull();
        var garlic = list.Items.Single(i => i.Name == "garlic");
        garlic.Quantity.Should().Be(6);
        garlic.Unit.Should().Be("clove");
        garlic.FromMealIds.Should().BeEquivalentTo(new[] { orzo.Id, barbacoa.Id });

        var toggled = await client.PostAsJsonAsync("/api/v1/shopping-list/items/toggle", new { listId = list.Id, name = "Garlic", unit = "clove" });
        toggled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await toggled.Content.ReadFromJsonAsync<ShoppingListDto>())!.Items.Single(i => i.Name == "garlic").Checked.Should().BeTrue();

        await client.PutAsJsonAsync("/api/v1/meals/week", new { weekStart = Monday, mealIds = new[] { orzo.Id } });
        var regenerated = (await (await client.PostAsJsonAsync("/api/v1/shopping-list/generate", new { weekStart = Monday })).Content.ReadFromJsonAsync<ShoppingListDto>())!;

        regenerated.Id.Should().Be(list.Id);
        regenerated.Items.Single(i => i.Name == "garlic").Should().BeEquivalentTo(new { Quantity = 2m, Unit = "clove", Checked = true });
        regenerated.Items.Should().NotContain(i => i.Name == "beef chuck");
        regenerated.CheckedCount.Should().Be(1);

        var fetched = (await client.GetFromJsonAsync<ShoppingListDto>($"/api/v1/shopping-list?week={Monday.AddDays(6):yyyy-MM-dd}"))!;
        fetched.Items.Should().BeEquivalentTo(regenerated.Items);
    }

    [Fact]
    public async Task ShoppingList_WithoutAList_IsEmptyWithNullId()
    {
        var client = NewUser();

        var list = (await client.GetFromJsonAsync<ShoppingListDto>($"/api/v1/shopping-list?week={Monday:yyyy-MM-dd}"))!;

        list.Id.Should().BeNull();
        list.WeekStart.Should().Be(Monday);
        list.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_TextAndCsv_AreByteExact_AndUnknownFormatIs400()
    {
        var client = NewUser();
        var created = await client.PostAsJsonAsync("/api/v1/meals", Orzo());
        var meal = (await created.Content.ReadFromJsonAsync<MealDto>())!;
        await client.PutAsJsonAsync("/api/v1/meals/week", new { weekStart = Monday, mealIds = new[] { meal.Id } });
        var list = (await (await client.PostAsJsonAsync("/api/v1/shopping-list/generate", new { weekStart = Monday })).Content.ReadFromJsonAsync<ShoppingListDto>())!;
        await client.PostAsJsonAsync("/api/v1/shopping-list/items/toggle", new { listId = list.Id, name = "garlic", unit = "clove" });

        var text = await client.GetAsync($"/api/v1/shopping-list/export?week={Monday:yyyy-MM-dd}&format=text");
        var csv = await client.GetAsync($"/api/v1/shopping-list/export?week={Monday:yyyy-MM-dd}&format=csv");
        var bad = await client.GetAsync($"/api/v1/shopping-list/export?week={Monday:yyyy-MM-dd}&format=pdf");

        text.StatusCode.Should().Be(HttpStatusCode.OK);
        text.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await text.Content.ReadAsStringAsync()).Should().Be("- [x] 2 clove garlic\n- [ ] 200 g orzo\n- [ ] salt");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        csv.Content.Headers.ContentDisposition!.FileName.Should().Be("\"shopping-list-2026-11-09.csv\"");
        (await csv.Content.ReadAsStringAsync()).Should().Be("name,quantity,unit,checked\ngarlic,2,clove,true\norzo,200,g,false\nsalt,,,false\n");
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnotherUser_CannotSeeOrToggleMyList_OrEditMyMeal()
    {
        var mine = NewUser();
        var theirs = NewUser();
        var meal = (await (await mine.PostAsJsonAsync("/api/v1/meals", Orzo())).Content.ReadFromJsonAsync<MealDto>())!;
        await mine.PutAsJsonAsync("/api/v1/meals/week", new { weekStart = Monday, mealIds = new[] { meal.Id } });
        var list = (await (await mine.PostAsJsonAsync("/api/v1/shopping-list/generate", new { weekStart = Monday })).Content.ReadFromJsonAsync<ShoppingListDto>())!;

        var toggle = await theirs.PostAsJsonAsync("/api/v1/shopping-list/items/toggle", new { listId = list.Id, name = "garlic", unit = "clove" });
        var edit = await theirs.PutAsJsonAsync($"/api/v1/meals/{meal.Id}", Orzo());
        var menu = await theirs.PutAsJsonAsync("/api/v1/meals/week", new { weekStart = Monday, mealIds = new[] { meal.Id } });
        var theirList = (await theirs.GetFromJsonAsync<ShoppingListDto>($"/api/v1/shopping-list?week={Monday:yyyy-MM-dd}"))!;

        toggle.StatusCode.Should().Be(HttpStatusCode.NotFound);
        edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        menu.StatusCode.Should().Be(HttpStatusCode.NotFound);
        theirList.Id.Should().BeNull();
    }

    [Fact]
    public async Task Unauthenticated_Is401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/meals");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
