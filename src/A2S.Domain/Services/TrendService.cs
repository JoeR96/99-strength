using A2S.Domain.Aggregates.BodyWeight;

namespace A2S.Domain.Services;

/// <summary>
/// Smooths morning weigh-ins into a trend and reads that trend against the plan's checkpoints.
/// </summary>
public sealed class TrendService
{
    public const decimal DefaultAlpha = 0.1m;
    public const decimal DefaultToleranceKg = 0.3m;

    /// <summary>
    /// Points ordered by date. <see cref="TrendPoint.MovingAverage7"/> is the mean of the entries in the trailing
    /// seven calendar days and is null until all seven days have one. The EWMA is seeded with the first weight.
    /// </summary>
    public IReadOnlyList<TrendPoint> Series(IReadOnlyList<BodyWeightEntry> entries, decimal alpha = DefaultAlpha)
    {
        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in (0, 1].");
        }

        var ordered = entries.OrderBy(e => e.Date).ToList();
        var points = new List<TrendPoint>(ordered.Count);
        decimal? ewma = null;

        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            ewma = ewma is null ? entry.Kg : alpha * entry.Kg + (1 - alpha) * ewma.Value;

            var windowStart = entry.Date.AddDays(-6);
            var window = ordered.Take(i + 1).Where(e => e.Date >= windowStart).Select(e => e.Kg).ToList();
            decimal? movingAverage = window.Count == 7 ? Math.Round(window.Average(), 2) : null;

            points.Add(new TrendPoint(entry.Date, entry.Kg, movingAverage, Math.Round(ewma.Value, 2)));
        }

        return points;
    }

    /// <summary>
    /// Reads <paramref name="trendKg"/> against the straight line through the start weight and the checkpoints.
    /// Before the start the target is the start weight; after the last checkpoint it holds.
    /// </summary>
    public PaceResult PaceAgainst(
        IReadOnlyList<(DateOnly Date, decimal Kg)> checkpoints,
        DateOnly startDate,
        decimal startKg,
        DateOnly today,
        decimal trendKg,
        decimal toleranceKg = DefaultToleranceKg)
    {
        if (checkpoints.Count == 0)
        {
            return new PaceResult(Pace.NoTarget, startKg, trendKg, Math.Round(trendKg - startKg, 2));
        }

        var line = new List<(DateOnly Date, decimal Kg)> { (startDate, startKg) };
        line.AddRange(checkpoints.Where(c => c.Date > startDate).OrderBy(c => c.Date));

        var target = TargetOn(line, today);
        var delta = Math.Round(trendKg - target, 2);
        var pace = delta > toleranceKg ? Pace.Behind
            : delta < -toleranceKg ? Pace.Ahead
            : Pace.OnPace;

        return new PaceResult(pace, target, trendKg, delta);
    }

    private static decimal TargetOn(IReadOnlyList<(DateOnly Date, decimal Kg)> line, DateOnly today)
    {
        if (today <= line[0].Date)
        {
            return line[0].Kg;
        }

        var last = line[^1];
        if (today >= last.Date)
        {
            return last.Kg;
        }

        for (var i = 0; i < line.Count - 1; i++)
        {
            var (fromDate, fromKg) = line[i];
            var (toDate, toKg) = line[i + 1];
            if (today < fromDate || today > toDate)
            {
                continue;
            }

            var span = toDate.DayNumber - fromDate.DayNumber;
            var elapsed = today.DayNumber - fromDate.DayNumber;
            return Math.Round(fromKg + (toKg - fromKg) * elapsed / span, 2);
        }

        return last.Kg;
    }
}
