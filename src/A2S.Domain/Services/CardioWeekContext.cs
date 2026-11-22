using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;

namespace A2S.Domain.Services;

/// <summary>
/// Reads the plan for a track's current week (brief § 4b.1): how many sessions the schedule plans for the
/// track each week (cardio slots pointing at it, and lift finishers pointing at it), and how many of those
/// were missed since the week started (a scheduled day before <c>date</c> with no completion for the track;
/// paused days don't count). Null when nothing in the schedule points at the track.
/// </summary>
public static class CardioWeekContext
{
    public static WeekContext? For(TrainingPlan? plan, CardioTrackId track, DateOnly date, DateOnly? weekStartedOn)
    {
        if (plan is null)
        {
            return null;
        }

        var slots = plan.Schedule
            .Where(s => (s.Kind == SessionKind.Cardio && s.CardioTrackId == track)
                || (s.Kind == SessionKind.Lift && s.Finisher?.CardioTrackId == track))
            .ToList();
        if (slots.Count == 0)
        {
            return null;
        }

        var from = weekStartedOn ?? date;
        if (from < plan.StartDate)
        {
            from = plan.StartDate;
        }

        var missed = 0;
        for (var day = from; day < date; day = day.AddDays(1))
        {
            if (plan.IsPaused(day))
            {
                continue;
            }

            var planned = slots.Count(s => s.Day == day.DayOfWeek);
            var done = plan.Completions.Count(c => c.Date == day && c.CardioTrackId == track);
            missed += Math.Max(0, planned - done);
        }

        return new WeekContext(slots.Count, missed);
    }
}
