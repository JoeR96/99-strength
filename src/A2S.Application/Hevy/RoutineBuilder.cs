using A2S.Application.Interfaces.Hevy;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.Hevy;

/// <summary>
/// Turns a scheduled session into the <see cref="HevyRoutineDraft"/> the push sends. Pure: every Hevy
/// template id is resolved beforehand (<see cref="HevyTemplateCatalogue"/>) and passed in.
/// Lifting: Mobility A drills first, then the day's exercises with their planned sets (AMRAP set as
/// <c>failure</c>), rest from <see cref="RestDefaults"/>, superset ids for paired accessories.
/// Cardio: one exercise, one set per interval (LISS: one distance/duration set). Mobility: the drills.
/// </summary>
public static class RoutineBuilder
{
    public static string LiftTitle(string planName, int week, string label) => $"{planName} · W{week} {label}";

    public static string SlotTitle(string planName, string label) => $"{planName} · {label}";

    public static HevyRoutineDraft Lift(
        string title,
        int? folderId,
        Workout workout,
        DayNumber day,
        WeekParameters week,
        MobilityRoutine? warmUp,
        IReadOnlyDictionary<string, string> drillTemplates,
        IReadOnlyDictionary<ExerciseId, string> exerciseTemplates,
        IReadOnlyList<SupersetSpec> supersets,
        IReadOnlyDictionary<string, string> exerciseNotes)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(week);
        ArgumentNullException.ThrowIfNull(drillTemplates);
        ArgumentNullException.ThrowIfNull(exerciseTemplates);
        ArgumentNullException.ThrowIfNull(supersets);
        ArgumentNullException.ThrowIfNull(exerciseNotes);

        var exercises = new List<HevyRoutineExerciseDraft>();
        if (warmUp is not null)
        {
            exercises.AddRange(DrillExercises(warmUp, drillTemplates));
        }

        var templateWeek = workout.GetTemplateWeek(workout.CurrentWeek);
        var blockType = workout.GetBlockType(workout.CurrentWeek);
        var pairs = supersets.Where(s => s.Day == day).ToList();

        foreach (var exercise in workout.GetExercisesForDay(day))
        {
            if (!exerciseTemplates.TryGetValue(exercise.Id, out var templateId))
            {
                throw new InvalidOperationException($"No Hevy template resolved for exercise '{exercise.Name}'");
            }

            var planned = exercise.CalculatePlannedSets(templateWeek, blockType).ToList();
            var supersetId = SupersetIdFor(exercise.Name, pairs);
            var rest = exercise.Category == ExerciseCategory.MainLift
                ? RestDefaults.MainLiftSeconds
                : supersetId is null ? RestDefaults.AccessorySeconds : RestDefaults.SupersetSeconds;

            var notes = new List<string>();
            var progressionNote = ExerciseNoteBuilder.Build(exercise, week, planned);
            if (!string.IsNullOrWhiteSpace(progressionNote))
            {
                notes.Add(progressionNote);
            }

            if (exerciseNotes.TryGetValue(exercise.Name, out var briefNote) && !string.IsNullOrWhiteSpace(briefNote))
            {
                notes.Add(briefNote);
            }

            exercises.Add(new HevyRoutineExerciseDraft
            {
                ExerciseTemplateId = templateId,
                SupersetId = supersetId,
                RestSeconds = rest,
                Notes = notes.Count == 0 ? null : string.Join(" | ", notes),
                Sets = planned.Select(set =>
                {
                    var kg = Math.Round(set.Weight.ConvertTo(WeightUnit.Kilograms).Value, 2);
                    return new HevySetDraft
                    {
                        Type = set.IsAmrap ? HevySetTypes.Failure : HevySetTypes.Normal,
                        WeightKg = kg > 0 ? kg : null,
                        Reps = set.TargetReps
                    };
                }).ToList()
            });
        }

        return new HevyRoutineDraft { Title = title, FolderId = folderId, Exercises = exercises };
    }

    public static HevyRoutineDraft Cardio(string title, int? folderId, CardioProtocol protocol, string templateId)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        var sets = protocol.Kind == CardioProtocolKind.Liss
            ? [new HevySetDraft { Type = HevySetTypes.Normal, DurationSeconds = protocol.Intervals.Sum(i => i.Seconds) }]
            : protocol.Intervals.Select(i => new HevySetDraft { Type = HevySetTypes.Normal, DurationSeconds = i.Seconds }).ToList();

        var sequence = string.Join(" · ", protocol.Intervals.Select(i => $"{i.Label} {Clock(i.Seconds)} ({EffortWord(i.Effort)})"));
        var notes = string.IsNullOrWhiteSpace(protocol.Notes) ? sequence : $"{protocol.Notes} | {sequence}";

        return new HevyRoutineDraft
        {
            Title = title,
            FolderId = folderId,
            Notes = protocol.Name,
            Exercises =
            [
                new HevyRoutineExerciseDraft { ExerciseTemplateId = templateId, RestSeconds = 0, Notes = notes, Sets = sets }
            ]
        };
    }

    public static HevyRoutineDraft Mobility(string title, int? folderId, MobilityRoutine routine, IReadOnlyDictionary<string, string> drillTemplates)
    {
        ArgumentNullException.ThrowIfNull(routine);
        return new HevyRoutineDraft
        {
            Title = title,
            FolderId = folderId,
            Notes = $"{routine.Name} · {routine.TargetMinutes} min",
            Exercises = DrillExercises(routine, drillTemplates)
        };
    }

    /// <summary>One routine exercise per drill: sets × dose, timed drills as duration sets, the rest as reps.</summary>
    public static List<HevyRoutineExerciseDraft> DrillExercises(MobilityRoutine routine, IReadOnlyDictionary<string, string> drillTemplates)
    {
        ArgumentNullException.ThrowIfNull(routine);
        ArgumentNullException.ThrowIfNull(drillTemplates);

        var result = new List<HevyRoutineExerciseDraft>();
        foreach (var drill in routine.Drills)
        {
            if (!drillTemplates.TryGetValue(drill.Name, out var templateId))
            {
                throw new InvalidOperationException($"No Hevy template resolved for drill '{drill.Name}'");
            }

            var setCount = drill.Sets + (drill.ExtraSetSide == SideBias.None ? 0 : 1);
            var set = drill.Dose.Unit == DoseUnit.Seconds
                ? new HevySetDraft { Type = HevySetTypes.Normal, DurationSeconds = drill.Dose.Amount }
                : new HevySetDraft { Type = HevySetTypes.Normal, Reps = drill.Dose.Amount };

            result.Add(new HevyRoutineExerciseDraft
            {
                ExerciseTemplateId = templateId,
                RestSeconds = 0,
                Notes = DrillNote(drill),
                Sets = Enumerable.Repeat(set, setCount).ToList()
            });
        }

        return result;
    }

    /// <summary>"2 × 5 breaths each side, extra set left · cue". What the Hevy set cannot say on its own.</summary>
    public static string DrillNote(MobilityDrill drill)
    {
        ArgumentNullException.ThrowIfNull(drill);

        var dose = drill.Dose.Unit switch
        {
            DoseUnit.Seconds => $"{drill.Dose.Amount} s",
            DoseUnit.Breaths => $"{drill.Dose.Amount} breaths",
            _ => $"{drill.Dose.Amount} reps"
        };

        var parts = new List<string> { $"{drill.Sets} × {dose}{(drill.PerSide ? " each side" : string.Empty)}" };
        if (drill.ExtraSetSide != SideBias.None)
        {
            parts.Add($"extra set {drill.ExtraSetSide.ToString().ToLowerInvariant()}");
        }

        var head = string.Join(", ", parts);
        var tail = new List<string>();
        if (drill.Cues is not null)
        {
            tail.Add(drill.Cues);
        }

        if (drill.StandingAlternative is not null)
        {
            tail.Add($"Standing: {drill.StandingAlternative}");
        }

        return tail.Count == 0 ? head : $"{head} · {string.Join(" · ", tail)}";
    }

    private static int? SupersetIdFor(string exerciseName, IReadOnlyList<SupersetSpec> pairs)
    {
        for (var i = 0; i < pairs.Count; i++)
        {
            if (string.Equals(pairs[i].First, exerciseName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pairs[i].Second, exerciseName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }

    private static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    private static string EffortWord(CardioEffort effort) => effort switch
    {
        CardioEffort.WarmUp => "warm-up",
        CardioEffort.Work => "work",
        CardioEffort.Recovery => "recovery",
        CardioEffort.CoolDown => "cool-down",
        _ => "easy"
    };
}
