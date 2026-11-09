using System.Globalization;
using System.Text;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.Meals;

/// <summary>How the shopping list is exported.</summary>
public enum ShoppingListExportFormat
{
    /// <summary>One line per item: <c>- [x] 2 clove garlic</c>.</summary>
    Text,

    /// <summary>Header <c>name,quantity,unit,checked</c>, RFC 4180 quoting.</summary>
    Csv
}

/// <summary>
/// Turns a shopping list into text to copy, share or download. Pure formatting, pinned byte for byte
/// by <c>ShoppingListExportTests</c>; the client renders exactly what it gets.
/// </summary>
public static class ShoppingListExport
{
    public const string CsvHeader = "name,quantity,unit,checked";
    public const string TextContentType = "text/plain; charset=utf-8";
    public const string CsvContentType = "text/csv; charset=utf-8";

    public static ShoppingListExportDto Export(ShoppingListDto list, ShoppingListExportFormat format) => format switch
    {
        ShoppingListExportFormat.Csv => new ShoppingListExportDto(Csv(list.Items), CsvContentType, FileName(list.WeekStart, "csv")),
        _ => new ShoppingListExportDto(Text(list.Items), TextContentType, FileName(list.WeekStart, "txt"))
    };

    /// <summary>One markdown-style task line per item, newline separated, no trailing newline.</summary>
    public static string Text(IEnumerable<ShoppingListItemDto> items) =>
        string.Join("\n", items.Select(i => $"- [{(i.Checked ? 'x' : ' ')}] {Label(i)}"));

    /// <summary>Header plus one row per item, each line ending in <c>\n</c>.</summary>
    public static string Csv(IEnumerable<ShoppingListItemDto> items)
    {
        var sb = new StringBuilder(CsvHeader).Append('\n');
        foreach (var item in items)
        {
            sb.Append(CsvField(item.Name)).Append(',')
              .Append(item.Quantity is { } q ? FormatQuantity(q) : string.Empty).Append(',')
              .Append(CsvField(item.Unit ?? string.Empty)).Append(',')
              .Append(item.Checked ? "true" : "false")
              .Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>"2 clove garlic", "200 g orzo", or just "salt" when unquantified.</summary>
    public static string Label(ShoppingListItemDto item) =>
        item.Quantity is { } q ? $"{FormatQuantity(q)} {item.Unit} {item.Name}" : item.Name;

    /// <summary>Up to three decimals, no trailing zeros, invariant culture: 6, 1.2, 0.5.</summary>
    public static string FormatQuantity(decimal quantity) => quantity.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FileName(DateOnly weekStart, string extension) =>
        $"shopping-list-{weekStart:yyyy-MM-dd}.{extension}";

    private static string CsvField(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
