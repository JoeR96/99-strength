using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.Meals;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.Meals;

/// <summary>Review focus 5 of the Phase 10 plan plus the byte-exact two-item fixtures for both formats.</summary>
public class ShoppingListExportTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly Guid Orzo = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly IReadOnlyList<ShoppingListItemDto> TwoItems = new[]
    {
        new ShoppingListItemDto("garlic", 6, "clove", true, new[] { Orzo }),
        new ShoppingListItemDto("salt", null, null, false, new[] { Orzo }),
    };

    [Fact]
    public void Text_TwoItems_IsByteExact()
    {
        ShoppingListExport.Text(TwoItems).Should().Be("- [x] 6 clove garlic\n- [ ] salt");
    }

    [Fact]
    public void Csv_TwoItems_IsByteExact()
    {
        ShoppingListExport.Csv(TwoItems).Should().Be("name,quantity,unit,checked\ngarlic,6,clove,true\nsalt,,,false\n");
    }

    [Fact]
    public void Csv_NameWithAComma_IsQuoted()
    {
        var items = new[]
        {
            new ShoppingListItemDto("chipotle peppers, in adobo", 3, "piece", false, Array.Empty<Guid>()),
            new ShoppingListItemDto("say \"cheese\"", null, null, true, Array.Empty<Guid>()),
        };

        ShoppingListExport.Csv(items).Should().Be(
            "name,quantity,unit,checked\n\"chipotle peppers, in adobo\",3,piece,false\n\"say \"\"cheese\"\"\",,,true\n");
    }

    [Theory]
    [InlineData(6, "6")]
    [InlineData(1.2, "1.2")]
    [InlineData(0.5, "0.5")]
    [InlineData(200, "200")]
    [InlineData(1.50, "1.5")]
    [InlineData(0.333, "0.333")]
    public void FormatQuantity_DropsTrailingZeros(decimal quantity, string expected)
    {
        ShoppingListExport.FormatQuantity(quantity).Should().Be(expected);
    }

    [Fact]
    public void Export_PicksContentTypeAndFileName()
    {
        var list = new ShoppingListDto(Guid.NewGuid(), Monday, new[] { Orzo }, TwoItems, 1, 2);

        var text = ShoppingListExport.Export(list, ShoppingListExportFormat.Text);
        var csv = ShoppingListExport.Export(list, ShoppingListExportFormat.Csv);

        text.ContentType.Should().Be("text/plain; charset=utf-8");
        text.FileName.Should().Be("shopping-list-2026-11-09.txt");
        text.Content.Should().StartWith("- [x] 6 clove garlic");
        csv.ContentType.Should().Be("text/csv; charset=utf-8");
        csv.FileName.Should().Be("shopping-list-2026-11-09.csv");
        csv.Content.Should().StartWith("name,quantity,unit,checked\n");
    }

    [Fact]
    public void EmptyList_ExportsHeaderOnlyCsv_AndEmptyText()
    {
        ShoppingListExport.Csv(Array.Empty<ShoppingListItemDto>()).Should().Be("name,quantity,unit,checked\n");
        ShoppingListExport.Text(Array.Empty<ShoppingListItemDto>()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportQuery_LoadsTheWeeksList_AndSnapsToMonday()
    {
        var lists = Substitute.For<IShoppingListRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<ILocalClock>();
        currentUser.UserId.Returns(Joe.Value);
        var list = ShoppingList.Create(Joe, Monday, Array.Empty<MealId>(), new[] { new ShoppingListItem("garlic", 6, "clove", true, Array.Empty<MealId>()) });
        lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);
        var handler = new ExportShoppingListQueryHandler(lists, currentUser, clock);

        var result = await handler.Handle(new ExportShoppingListQuery(Monday.AddDays(4), ShoppingListExportFormat.Text), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("- [x] 6 clove garlic");
        clock.DidNotReceive().Today();
    }

    [Fact]
    public async Task ExportQuery_NoWeek_UsesTodaysWeek_AndNoList_IsEmpty()
    {
        var lists = Substitute.For<IShoppingListRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<ILocalClock>();
        currentUser.UserId.Returns(Joe.Value);
        clock.Today().Returns(Monday.AddDays(6));
        lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns((ShoppingList?)null);
        var handler = new ExportShoppingListQueryHandler(lists, currentUser, clock);

        var result = await handler.Handle(new ExportShoppingListQuery(null, ShoppingListExportFormat.Csv), CancellationToken.None);

        result.Value.Content.Should().Be("name,quantity,unit,checked\n");
        result.Value.FileName.Should().Be("shopping-list-2026-11-09.csv");
    }
}
