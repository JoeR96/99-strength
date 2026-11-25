using System.Globalization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Cardio;

/// <summary>
/// One line for a logged cardio exercise in history: "{work} · {Effort}", e.g. "7 min @ level 6 · Easy".
/// The work is the planned title, with the logged work minutes swapped in for a continuous block
/// (intervals keep the planned title). Adds " · stopped early" and " · pain" when they apply.
/// </summary>
public static class CardioSummaryFormatter
{
    /// <summary>The summary of a cardio performance; null for a lift (no cardio result).</summary>
    public static string? For(ExercisePerformance performance) =>
        performance.Cardio is { } cardio ? Format(performance.PlannedCardio, cardio) : null;

    public static string Format(PlannedCardioBlock? planned, CardioResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var parts = new List<string>();
        if (Work(planned, result) is { } work)
        {
            parts.Add(work);
        }

        parts.Add(result.Effort.ToString());
        if (!result.Finished)
        {
            parts.Add("stopped early");
        }

        if (result.Pain != PainFlag.None)
        {
            parts.Add("pain");
        }

        return string.Join(" · ", parts);
    }

    private static string? Work(PlannedCardioBlock? planned, CardioResult result)
    {
        if (planned is null)
        {
            return result.WorkMinutes is { } logged ? $"{Number(logged)} min" : null;
        }

        var plannedMinutes = $"{planned.WorkMinutes} min";
        if (planned.Kind == CardioStageKind.Continuous && result.WorkMinutes is { } minutes && planned.Title.StartsWith(plannedMinutes, StringComparison.Ordinal))
        {
            return $"{Number(minutes)} min" + planned.Title[plannedMinutes.Length..];
        }

        return planned.Title;
    }

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
