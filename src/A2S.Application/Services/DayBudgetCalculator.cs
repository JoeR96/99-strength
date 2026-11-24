using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.Services;

/// <summary>
/// Glues the Domain volume budget and time estimate to the exercise library.
/// Muscle groups come from <see cref="IExerciseLibraryProvider"/> (matched by name);
/// rests come from <see cref="RestDefaults"/> by exercise category. The Workout has no
/// superset concept yet, so every exercise is timed on its own.
/// </summary>
public sealed class DayBudgetCalculator
{
    private readonly VolumeBudgetService _volume;
    private readonly IExerciseLibraryProvider _library;

    public DayBudgetCalculator(VolumeBudgetService volume, IExerciseLibraryProvider library)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    /// <summary>
    /// Weekly hard sets across the whole workout plus the time estimate for <paramref name="day"/>.
    /// </summary>
    public async Task<DayBudgetDto> ForDayAsync(Workout workout, DayNumber day, int weekNumber, CancellationToken ct = default)
    {
        var lookup = await BuildLookupAsync(ct);
        var sets = _volume.HardSetsPerGroup(workout, weekNumber, lookup);
        var warnings = _volume.Warnings(sets, VolumeCaps.Default);
        var time = EstimateDay(workout, day, weekNumber);

        return new DayBudgetDto(
            sets.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            warnings.Select(ToDto).ToList(),
            ToDto(time));
    }

    /// <summary>
    /// What a command result carries after a change: the week's volume warnings and whether any
    /// of the affected days is now over the time budget. Uses the workout's current week.
    /// </summary>
    public async Task<(IReadOnlyList<VolumeWarningDto> Warnings, bool OverTimeBudget)> AfterChangeAsync(
        Workout workout,
        IEnumerable<DayNumber> affectedDays,
        CancellationToken ct = default)
    {
        var weekNumber = CurrentWeekOf(workout);
        var lookup = await BuildLookupAsync(ct);
        var sets = _volume.HardSetsPerGroup(workout, weekNumber, lookup);
        var warnings = _volume.Warnings(sets, VolumeCaps.Default).Select(ToDto).ToList();
        var overTime = affectedDays.Distinct().Any(day => EstimateDay(workout, day, weekNumber).OverBudget);

        return (warnings, overTime);
    }

    /// <summary>The workout's current week clamped into the program range.</summary>
    public static int CurrentWeekOf(Workout workout) =>
        Math.Clamp(workout.CurrentWeek, 1, workout.TotalWeeks);

    /// <summary>
    /// Lifts are timed by sets and rests; a cardio exercise adds its block's <c>TotalMinutes</c>
    /// (warm-up + work + cool-down) after them.
    /// </summary>
    private static SessionTimeEstimate EstimateDay(Workout workout, DayNumber day, int weekNumber)
    {
        var templateWeek = workout.GetTemplateWeek(weekNumber);
        var blockType = workout.GetBlockType(weekNumber);
        var exercises = workout.GetExercisesForDay(day)
            .Select(exercise => (Exercise: exercise, Cardio: exercise.Progression.CalculateCardioBlock()))
            .ToList();
        var cardio = exercises
            .Where(pair => pair.Cardio is not null)
            .Select(pair => (pair.Exercise.Name, Minutes: pair.Cardio!.TotalMinutes))
            .ToList();

        var timings = exercises.Where(pair => pair.Cardio is null).Select(pair =>
        {
            var exercise = pair.Exercise;
            var sets = exercise.CalculatePlannedSets(templateWeek, blockType).Count();
            if (exercise.IsUnilateral())
            {
                sets *= 2;
            }

            var rest = exercise.Category == ExerciseCategory.Accessory
                ? RestDefaults.AccessorySeconds
                : RestDefaults.MainLiftSeconds;

            return new ExerciseTiming(exercise.Name, sets, rest, null);
        });

        var lifts = SessionTimeEstimator.Estimate(timings);
        if (cardio.Count == 0)
        {
            return lifts;
        }

        var total = lifts.TotalMinutes + cardio.Sum(c => c.Minutes);
        return lifts with
        {
            TotalMinutes = total,
            PerExercise = lifts.PerExercise.Concat(cardio).ToList(),
            OverBudget = total > lifts.BudgetMinutes
        };
    }

    private async Task<Func<string, (string Primary, IReadOnlyList<string> Secondary)?>> BuildLookupAsync(CancellationToken ct)
    {
        var templates = await _library.GetAllTemplatesAsync(ct);
        var byName = new Dictionary<string, ExerciseTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            byName.TryAdd(template.Name, template);
        }

        return key =>
        {
            if (!byName.TryGetValue(key, out var template) || string.IsNullOrWhiteSpace(template.MuscleGroup))
            {
                return null;
            }

            return (template.MuscleGroup, template.SecondaryMuscleGroups);
        };
    }

    private static VolumeWarningDto ToDto(VolumeWarning warning) =>
        new(warning.Bucket.ToString(), warning.Sets, warning.Cap, warning.Message);

    private static SessionTimeEstimateDto ToDto(SessionTimeEstimate estimate) =>
        new(
            estimate.TotalMinutes,
            estimate.WarmUpMinutes,
            estimate.PerExercise.Select(e => new ExerciseTimeDto(e.Name, e.Minutes)).ToList(),
            estimate.OverBudget,
            estimate.BudgetMinutes);
}
