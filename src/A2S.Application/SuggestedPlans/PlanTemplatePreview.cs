using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.SuggestedPlans;

/// <summary>
/// Previews a template for a start date without creating anything: phases with dates, the week, each
/// session's exercises (2a/2b superset labels), warm-ups, home routines, cardio tracks and what is
/// left to fill in. Pure.
/// </summary>
public static class PlanTemplatePreview
{
    /// <summary>Any valid value previews a required training max; the preview never shows weights.</summary>
    private const decimal PreviewTrainingMax = 100m;

    public static SuggestedPlanSpec PreviewSpec(IPlanTemplate template, DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(template);
        var values = template.Inputs.ToDictionary(
            i => i.Key,
            i => i.Key == "startWeightKg" ? i.Default : i.Optional ? null : i.Default ?? (decimal?)PreviewTrainingMax,
            StringComparer.OrdinalIgnoreCase);
        return template.Build(new PlanTemplateInputs(values, template.DefaultStartWeightKg, startDate));
    }

    public static SuggestedPlanPreviewDto Build(IPlanTemplate template, DateOnly startDate, bool alreadyStarted)
    {
        var spec = PreviewSpec(template, startDate);
        var slotsByDay = spec.Schedule.Where(s => s.LiftDay is not null)
            .GroupBy(s => s.LiftDay!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var days = slotsByDay.Keys.OrderBy(d => d).ToList();

        var sessions = days.Select(day =>
        {
            var slot = slotsByDay[day];
            var exercises = Ordered(spec, day);
            return new TemplateSessionPreviewDto(
                slot.Label,
                day,
                slot.WarmUp,
                FinisherText(slot.Finisher),
                Estimate(exercises).TotalMinutes,
                exercises.Select(x => new TemplateExercisePreviewDto(
                    x.Slot, x.Request.TemplateName, Prescription(x.Request), Progression(x.Request),
                    spec.ExerciseNotes.TryGetValue(x.Request.TemplateName, out var note) ? note : null)).ToList());
        }).ToList();

        var liftingDays = spec.LiftingDays.Count > 0
            ? spec.LiftingDays.Select(d => new SuggestedDayPreviewDto(
                d.Label,
                spec.Exercises.Where(e => e.AssignedDay == d.Day).OrderBy(e => e.OrderInDay).Select(e => e.TemplateName).ToList(),
                d.Estimate.TotalMinutes,
                d.Estimate.OverBudget)).ToList()
            : days.Select(day =>
            {
                var ordered = Ordered(spec, day);
                var estimate = Estimate(ordered);
                return new SuggestedDayPreviewDto(slotsByDay[day].Label, ordered.Select(x => x.Request.TemplateName).ToList(),
                    estimate.TotalMinutes, estimate.OverBudget);
            }).ToList();

        var weeks = spec.Phases.Count == 0 ? 0 : (spec.Phases.Max(p => p.To).DayNumber - spec.StartDate.DayNumber + 7) / 7;
        var blocks = spec.BlockSequence ?? [1, 2, 3];

        return new SuggestedPlanPreviewDto(
            spec.Key,
            spec.Name,
            spec.Description,
            spec.StartDate,
            template.Inputs,
            spec.Schedule.Select(s => new SuggestedSessionPreviewDto(s.Day, s.Kind, s.Label)).ToList(),
            liftingDays,
            spec.Habits.Select(h => h.Name).ToList(),
            spec.Checkpoints,
            alreadyStarted)
        {
            Phases = spec.Phases,
            Sessions = sessions,
            Slots = spec.Schedule.Select(s => new TemplateSlotPreviewDto(
                s.Day, s.Kind, s.Label, s.WarmUp, FinisherText(s.Finisher), s.MobilityRoutine, TrackName(s.CardioTrack))).ToList(),
            Routines = spec.Routines.Select(r => new TemplateRoutinePreviewDto(
                r.Name, r.TargetMinutes, r.Drills.Select(d => new TemplateDrillPreviewDto(d.Name, Dose(d), d.Cues)).ToList())).ToList(),
            CardioTracks = (spec.CardioTracks ?? [])
                .Select(k => CardioTrackTemplates.All.FirstOrDefault(t => t.Key == k))
                .OfType<CardioTrackTemplate>()
                .Select(t => new TemplateTrackPreviewDto(t.Key, t.Name, t.Description)).ToList(),
            ToFillIn = ToFillIn(spec),
            BlockSequence = blocks,
            ProgramWeeks = weeks > 0 ? weeks : blocks.Count * 7
        };
    }

    private sealed record SlotExercise(string Slot, CreateExerciseRequest Request, int? Group);

    /// <summary>Numbers the day: a solo exercise or a superset pair takes the next number; a pair is "2a"/"2b".</summary>
    private static List<SlotExercise> Ordered(SuggestedPlanSpec spec, DayNumber day)
    {
        var result = new List<SlotExercise>();
        var number = 0;
        int? openGroup = null;
        var letter = 'a';
        var pairs = spec.Supersets.ToList();
        foreach (var e in spec.Exercises.Where(e => e.AssignedDay == day).OrderBy(e => e.OrderInDay))
        {
            var specPair = pairs.FindIndex(s => s.Day == day
                && (string.Equals(s.First, e.TemplateName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Second, e.TemplateName, StringComparison.OrdinalIgnoreCase)));
            var group = e.SupersetGroup ?? (specPair >= 0 ? 100 + specPair : null);
            if (group is null)
            {
                openGroup = null;
                result.Add(new SlotExercise((++number).ToString(), e, null));
                continue;
            }

            if (group != openGroup)
            {
                openGroup = group;
                number++;
                letter = 'a';
            }

            result.Add(new SlotExercise($"{number}{letter++}", e, group));
        }

        return result;
    }

    private static SessionTimeEstimate Estimate(List<SlotExercise> exercises)
    {
        var mainLiftSets = A2SHypertrophyProgram.GetWeekData(1, ProgramTier.Primary).Sets;
        return SessionTimeEstimator.Estimate(exercises.Select(x =>
        {
            var e = x.Request;
            var sets = e.ProgressionType == "Linear" ? mainLiftSets : e.StartingSets ?? 3;
            if (e.IsUnilateral)
            {
                sets *= 2;
            }

            var rest = e.Category == ExerciseCategory.MainLift ? RestDefaults.MainLiftSeconds
                : x.Group is null ? RestDefaults.AccessorySeconds : RestDefaults.SupersetSeconds;
            return new ExerciseTiming(e.TemplateName, sets, rest, x.Group);
        }));
    }

    private static string Prescription(CreateExerciseRequest e)
    {
        if (e.ProgressionType == "Linear")
        {
            return $"{A2SHypertrophyProgram.GetWeekData(1, e.Tier ?? ProgramTier.Primary).Sets} sets, last set AMRAP";
        }

        var reps = e.RepRangeMinimum == e.RepRangeMaximum ? $"{e.RepRangeMinimum}" : $"{e.RepRangeMinimum}–{e.RepRangeMaximum}";
        var side = e.IsUnilateral ? " each side, left first" : string.Empty;
        var extra = e.ExtraSetSide == ExtraSetSide.None ? string.Empty : $", extra set {e.ExtraSetSide.ToString().ToLowerInvariant()}";
        return $"{e.StartingSets ?? 3} × {reps}{side}{extra}";
    }

    private static string Progression(CreateExerciseRequest e) => e.ProgressionType switch
    {
        "Linear" => e.Tier == ProgramTier.Auxiliary ? "A2S2 auxiliary scheme" : "A2S2 main lift",
        "MinimalSets" => "Minimal sets",
        _ => "Reps per set"
    };

    private static string? FinisherText(FinisherSpec? finisher) => finisher switch
    {
        null => null,
        { Fixed: { } activity } => $"{activity.DisplayName()}, {activity.Describe()}",
        { TrackKey: { } key } => TrackName(key),
        _ => null
    };

    private static string? TrackName(string? key) =>
        key is null ? null : CardioTrackTemplates.All.FirstOrDefault(t => t.Key == key)?.Name ?? key;

    private static string Dose(MobilityDrillDto drill)
    {
        var amount = drill.Dose.Unit switch
        {
            DoseUnit.Seconds => $"{drill.Dose.Amount} s",
            DoseUnit.Breaths => $"{drill.Dose.Amount} breaths",
            _ => $"{drill.Dose.Amount}"
        };

        var sets = drill.Sets > 1 ? $"{drill.Sets} × {amount}" : amount;
        var side = drill.PerSide ? " each side" : string.Empty;
        var extra = drill.ExtraSetSide == SideBias.None ? string.Empty : $", +{drill.ExtraSetSide.ToString()[0]}";
        return $"{sets}{side}{extra}";
    }

    private static IReadOnlyList<string> ToFillIn(SuggestedPlanSpec spec)
    {
        var items = spec.Exercises
            .Where(e => e.ProgressionType == "Linear" && e.TrainingMaxPending)
            .Select(e => $"Training max: {e.TemplateName} (optional now; set it before its first session)")
            .ToList();

        var noWeight = spec.Exercises.Count(e => e.ProgressionType == "RepsPerSet" && e.StartingWeight is null);
        if (noWeight > 0)
        {
            items.Add($"Starting weights for {noWeight} accessories (entered at the first session)");
        }

        return items;
    }
}
