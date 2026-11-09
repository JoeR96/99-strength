using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;

namespace A2S.Application.Hevy;

/// <summary>Where a pulled Hevy workout belongs in the plan, or why it does not.</summary>
public abstract record MatchResult
{
    /// <summary>The session the workout completes, on the local date it was done.</summary>
    public sealed record Matched(ScheduledSession Session, DateOnly Date, string How) : MatchResult;

    public sealed record Unmatched(string Reason, DateOnly Date) : MatchResult;
}

/// <summary>
/// Pure matching of a Hevy workout to a scheduled session. Order: (1) the workout's <c>routine_id</c>
/// is one the app pushed, so its link says which session; (2) same local date and the title contains
/// a session label; (3) mobility-only or cardio-only workouts (every exercise is one of the app's
/// drill or cardio templates) take that day's mobility or cardio session; (4) same local date, a
/// single lifting session, and at least 60 % of its planned exercise templates present.
/// The local date comes from <see cref="ILocalClock"/>, so a 23:30Z finish on a BST Friday is Saturday.
/// </summary>
public static class WorkoutMatcher
{
    public const double TemplateOverlapThreshold = 0.6;

    private const string DrillPrefix = "drill:";
    private const string CardioPrefix = "cardio:";

    /// <param name="workout">The Hevy workout as pulled.</param>
    /// <param name="plan">The user's active plan.</param>
    /// <param name="routineLinks">The user's <see cref="HevyLinkKind.Routine"/> links.</param>
    /// <param name="liftingProgram">The plan's linked A2S2 workout, for exercise overlap; null when none.</param>
    /// <param name="templateLinks">Hevy template id → local key for every <see cref="HevyLinkKind.Template"/> link.</param>
    /// <param name="clock">Turns the workout's start into a local date.</param>
    public static MatchResult Match(
        HevyWorkout workout,
        TrainingPlan plan,
        IReadOnlyList<HevyLink> routineLinks,
        Workout? liftingProgram,
        IReadOnlyDictionary<string, string> templateLinks,
        ILocalClock clock)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(routineLinks);
        ArgumentNullException.ThrowIfNull(templateLinks);
        ArgumentNullException.ThrowIfNull(clock);

        var date = clock.ToLocalDate(workout.StartTime);
        if (date < plan.StartDate)
        {
            return new MatchResult.Unmatched("before the plan started", date);
        }

        var sessions = plan.SessionsOn(date);
        var active = sessions.Where(s => s.Kind != SessionKind.Rest).ToList();

        // (1) A routine the app pushed: the link's key names the session.
        if (!string.IsNullOrEmpty(workout.RoutineId))
        {
            var link = routineLinks.FirstOrDefault(l => string.Equals(l.HevyId, workout.RoutineId, StringComparison.Ordinal));
            if (link is not null && HevyLocalKeys.TryParseRoutine(link.LocalKey, out var key) && key.PlanId == plan.Id)
            {
                var session = active.FirstOrDefault(key.Describes) ?? plan.Schedule.FirstOrDefault(key.Describes);
                if (session is not null)
                {
                    return new MatchResult.Matched(session, date, "routine");
                }
            }
        }

        if (active.Count == 0)
        {
            return new MatchResult.Unmatched(sessions.Count > 0 ? "rest day" : "nothing scheduled", date);
        }

        // (2) The title names the session.
        var byTitle = active.FirstOrDefault(s => workout.Title.Contains(s.Label, StringComparison.OrdinalIgnoreCase));
        if (byTitle is not null)
        {
            return new MatchResult.Matched(byTitle, date, "title");
        }

        // (3) Every exercise is one of the app's drill (or cardio) templates.
        var templateIds = workout.Exercises.Select(e => e.ExerciseTemplateId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (templateIds.Count > 0 && templateIds.All(id => HasPrefix(templateLinks, id, DrillPrefix)))
        {
            var mobility = active.FirstOrDefault(s => s.Kind == SessionKind.Mobility);
            return mobility is not null
                ? new MatchResult.Matched(mobility, date, "mobility templates")
                : new MatchResult.Unmatched("mobility-only workout with no mobility session that day", date);
        }

        if (templateIds.Count > 0 && templateIds.All(id => HasPrefix(templateLinks, id, CardioPrefix)))
        {
            var cardio = active.FirstOrDefault(s => s.Kind == SessionKind.Cardio);
            return cardio is not null
                ? new MatchResult.Matched(cardio, date, "cardio template")
                : new MatchResult.Unmatched("cardio workout with no cardio session that day", date);
        }

        // (4) One lifting session that day and most of its exercises present.
        var lifts = active.Where(s => s.Kind == SessionKind.Lift).ToList();
        if (lifts.Count == 1 && liftingProgram is not null && lifts[0].LiftDay is { } liftDay)
        {
            var planned = liftingProgram.GetExercisesForDay(liftDay)
                .Select(e => HevyTemplateIdFor(e, templateLinks))
                .Where(id => id is not null)
                .Select(id => id!)
                .ToList();

            if (planned.Count > 0)
            {
                var present = planned.Count(id => templateIds.Contains(id, StringComparer.Ordinal));
                if ((double)present / planned.Count >= TemplateOverlapThreshold)
                {
                    return new MatchResult.Matched(lifts[0], date, "exercise overlap");
                }

                return new MatchResult.Unmatched($"only {present} of {planned.Count} planned exercises present", date);
            }
        }

        return lifts.Count > 1
            ? new MatchResult.Unmatched("two lifting sessions that day; title does not say which", date)
            : new MatchResult.Unmatched("no session fits (title or exercises)", date);
    }

    /// <summary>The Hevy template id a planned exercise appears under: its library id, or its custom template's link.</summary>
    public static string? HevyTemplateIdFor(Exercise exercise, IReadOnlyDictionary<string, string> templateLinks)
    {
        if (!HevyTemplateCatalogue.IsCustom(exercise.ExternalTemplateId))
        {
            return exercise.ExternalTemplateId;
        }

        var key = HevyTemplateCatalogue.ExerciseTemplateKey(exercise.ExternalTemplateId);
        return templateLinks.FirstOrDefault(pair => string.Equals(pair.Value, key, StringComparison.Ordinal)).Key;
    }

    private static bool HasPrefix(IReadOnlyDictionary<string, string> templateLinks, string templateId, string prefix) =>
        templateLinks.TryGetValue(templateId, out var localKey) && localKey.StartsWith(prefix, StringComparison.Ordinal);
}
