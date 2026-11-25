using System.Globalization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Cardio;

/// <summary>
/// A cardio block as Hevy notes show it: the block's text, except that settings still blank (a walk's incline or
/// speed, an interval stage's hard level) are left out instead of shown as "?", e.g. "10 min incline walk".
/// </summary>
public static class CardioHevyNote
{
    public static string For(PlannedCardioBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!block.IsSettingsPending)
        {
            return block.Text;
        }

        var parts = new List<string>();
        if (block.WarmUp is { } warmUp)
        {
            parts.Add($"{warmUp.Minutes} min easy (L{Number(warmUp.Level)})");
        }

        parts.Add(block.Intervals is { } format ? Rounds(block, format) : Continuous(block));
        if (block.CoolDown is { } coolDown)
        {
            parts.Add($"{coolDown.Minutes} min easy (L{Number(coolDown.Level)})");
        }

        return string.Join(" → ", parts);
    }

    private static string Continuous(PlannedCardioBlock block)
    {
        var setting = (block.IntensityUnit, block.Intensity) switch
        {
            (IntensityUnit.Level, { } level) => $" @ level {Number(level)}",
            (IntensityUnit.InclinePercent, { } incline) => $" @ {Number(incline)} % incline",
            (IntensityUnit.InclinePercent, null) => " incline walk",
            (IntensityUnit.SpeedKmh, { } kmh) => $" @ {Number(kmh)} km/h",
            _ => string.Empty
        };
        var speed = block.IntensityUnit != IntensityUnit.SpeedKmh && block.Speed is { } s ? $", {Number(s)} km/h" : string.Empty;
        return $"{block.WorkMinutes} min{setting}{speed}";
    }

    private static string Rounds(PlannedCardioBlock block, A2S.Domain.Aggregates.Workout.Cardio.IntervalFormat format)
    {
        var hard = block.Intensity is { } level ? $"@ L{Number(level)}" : "hard";
        var easy = block.RecoveryLevel is { } recovery ? $"@ L{Number(recovery)}" : "easy";
        return $"{format.Rounds} × {format.HardMinutes} min {hard} / {format.EasyMinutes} min {easy}";
    }

    private static string Number(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);
}
