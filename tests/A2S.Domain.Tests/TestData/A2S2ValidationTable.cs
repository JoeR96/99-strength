using System.Globalization;

namespace A2S.Domain.Tests.TestData;

/// <summary>
/// One week of a lift's table in <c>research/A2S2_Validation_Data.md</c>.
/// </summary>
/// <param name="Week">Program week 1–21.</param>
/// <param name="TmAtStart">The TM the spreadsheet shows for that week (the TM the week is lifted at).</param>
/// <param name="RepOutTarget">The AMRAP target, null on deload weeks ("n/a").</param>
/// <param name="AmrapResult">Reps actually done on the AMRAP set, null when nothing was entered ("—").</param>
/// <param name="Multiplier">The TM multiplier the spreadsheet applied after the week (1.0 when nothing changes).</param>
/// <param name="IsDeload">True when the row is a deload week ("n/a" rep-out). Rows the table leaves as "..." are neither.</param>
public sealed record A2S2ValidationRow(int Week, decimal TmAtStart, int? RepOutTarget, int? AmrapResult, decimal Multiplier, bool IsDeload)
{
    /// <summary>actual − target, or null when the week has no AMRAP result.</summary>
    public int? Delta => RepOutTarget is { } target && AmrapResult is { } actual ? actual - target : null;
}

/// <summary>
/// Reads the OHP and Smith Squat tables straight out of <c>research/A2S2_Validation_Data.md</c>
/// (linked into the test output as <c>TestData/A2S2_Validation_Data.md</c>) so the cycle tests
/// cannot drift from the research document.
/// </summary>
public static class A2S2ValidationTable
{
    public const string OverheadPressHeading = "## Overhead Press";
    public const string SmithSquatHeading = "## Smith Squat";

    private static readonly Lazy<string[]> Lines = new(() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "A2S2_Validation_Data.md")));

    public static IReadOnlyList<A2S2ValidationRow> OverheadPress => Parse(OverheadPressHeading);

    public static IReadOnlyList<A2S2ValidationRow> SmithSquat => Parse(SmithSquatHeading);

    public static IReadOnlyList<A2S2ValidationRow> For(string heading) => Parse(heading);

    /// <summary>The "Initial TM: 65kg" line under a heading.</summary>
    public static decimal InitialTm(string heading)
    {
        var line = Section(heading).First(l => l.Contains("Initial TM", StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(line, @"Initial TM:\s*([\d.]+)kg");
        return decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<A2S2ValidationRow> Parse(string heading)
    {
        var rows = new List<A2S2ValidationRow>();
        var tableLines = Section(heading)
            .SkipWhile(l => !l.StartsWith("| Week", StringComparison.Ordinal))
            .Skip(2) // header + separator
            .TakeWhile(l => l.StartsWith('|'));

        foreach (var line in tableLines)
        {
            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim().Replace("**", string.Empty)).ToList();
            var (firstWeek, lastWeek) = ParseWeekRange(cells[0]);
            var tm = decimal.Parse(cells[1], CultureInfo.InvariantCulture);
            var repOut = ParseOptionalInt(cells[4]);
            var amrap = ParseOptionalInt(cells[6]);
            var multiplier = ParseMultiplier(cells);
            var isDeload = string.Equals(cells[4], "n/a", StringComparison.OrdinalIgnoreCase);

            for (var week = firstWeek; week <= lastWeek; week++)
            {
                rows.Add(new A2S2ValidationRow(week, tm, repOut, amrap, multiplier, isDeload));
            }
        }

        return rows;
    }

    private static IEnumerable<string> Section(string heading)
    {
        var lines = Lines.Value;
        var start = Array.FindIndex(lines, l => l.StartsWith(heading, StringComparison.Ordinal));
        if (start < 0)
        {
            throw new InvalidOperationException($"Heading '{heading}' not found in A2S2_Validation_Data.md");
        }

        for (var i = start + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++)
        {
            yield return lines[i];
        }
    }

    private static (int First, int Last) ParseWeekRange(string cell)
    {
        var parts = cell.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var first = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var last = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : first;
        return (first, last);
    }

    private static int? ParseOptionalInt(string cell) =>
        int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// The first cell carrying a percentage ("+3%", "-5%", "0%") is the applied change;
    /// "—", "no change" and "DELOAD" mean the TM carried forward.
    /// </summary>
    private static decimal ParseMultiplier(IReadOnlyList<string> cells)
    {
        foreach (var cell in cells.Skip(7))
        {
            var match = System.Text.RegularExpressions.Regex.Match(cell, @"^([+-]?[\d.]+)%$");
            if (match.Success)
            {
                return 1m + decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 100m;
            }
        }

        return 1m;
    }
}
