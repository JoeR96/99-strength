using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Services;

/// <summary>
/// Turns a <see cref="TrainingPlan"/> plus the linked workout's lifting history into calendar days,
/// weeks and weekly goals. Every date is a Europe/London local date from <see cref="ILocalClock"/>.
/// </summary>
public sealed class PlanCalendarService
{
    private readonly ILocalClock _clock;

    public PlanCalendarService(ILocalClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// The sessions scheduled for a date with their status. A lift counts as done when a lift completion
    /// exists for the date or a <see cref="WorkoutActivity"/> for the same A2S2 day was completed on that
    /// local date. Inside a Pause phase an undone session is Paused, never Missed.
    /// </summary>
    public PlanDay DayFor(TrainingPlan plan, DateOnly date, IReadOnlyCollection<WorkoutActivity> liftingHistory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(liftingHistory);

        var today = _clock.Today();
        var phase = plan.PhaseOn(date)?.Type;
        var paused = phase == PhaseType.Pause;
        var completionsToday = plan.Completions.Where(c => c.Date == date).ToList();
        var claimed = new HashSet<SessionCompletionId>();

        var sessions = new List<PlanDaySession>();
        foreach (var session in plan.SessionsOn(date))
        {
            if (session.Kind == SessionKind.Rest)
            {
                sessions.Add(new PlanDaySession(session, SessionStatus.Rest, null));
                continue;
            }

            var completion = completionsToday.FirstOrDefault(c => !claimed.Contains(c.Id) && Matches(session, c));
            if (completion is not null)
            {
                claimed.Add(completion.Id);
            }

            var doneFromHistory = session.Kind == SessionKind.Lift
                && liftingHistory.Any(a => a.Day == session.LiftDay && _clock.ToLocalDate(a.CompletedAt) == date);

            SessionStatus status;
            if (completion is not null || doneFromHistory)
            {
                status = SessionStatus.Done;
            }
            else if (paused)
            {
                status = SessionStatus.Paused;
            }
            else if (date < today)
            {
                status = SessionStatus.Missed;
            }
            else
            {
                status = SessionStatus.Planned;
            }

            sessions.Add(new PlanDaySession(session, status, completion));
        }

        return new PlanDay(date, phase, sessions);
    }

    public IReadOnlyList<PlanDay> Month(TrainingPlan plan, int year, int month, IReadOnlyCollection<WorkoutActivity> liftingHistory)
    {
        var days = DateTime.DaysInMonth(year, month);
        return Enumerable.Range(1, days)
            .Select(day => DayFor(plan, new DateOnly(year, month, day), liftingHistory))
            .ToList();
    }

    /// <summary>The Monday-to-Sunday week containing <paramref name="anyDay"/>.</summary>
    public IReadOnlyList<PlanDay> Week(TrainingPlan plan, DateOnly anyDay, IReadOnlyCollection<WorkoutActivity> liftingHistory)
    {
        var monday = LocalDates.StartOfWeek(anyDay);
        return Enumerable.Range(0, 7)
            .Select(offset => DayFor(plan, monday.AddDays(offset), liftingHistory))
            .ToList();
    }

    /// <summary>
    /// Weekly goals: planned counts leave out Paused sessions; done counts include completions from either
    /// source; allowance uses are counted within the week.
    /// </summary>
    public WeeklyGoals ForWeek(TrainingPlan plan, DateOnly anyDay, IReadOnlyCollection<WorkoutActivity> liftingHistory)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var weekStart = LocalDates.StartOfWeek(anyDay);
        var weekEnd = weekStart.AddDays(6);
        var sessions = Week(plan, anyDay, liftingHistory).SelectMany(d => d.Sessions).ToList();

        int Planned(SessionKind kind) => sessions.Count(s => s.Session.Kind == kind && s.Status != SessionStatus.Paused);
        int Done(SessionKind kind) => sessions.Count(s => s.Session.Kind == kind && s.Status == SessionStatus.Done);
        int Used(AllowanceKind kind) => plan.AllowanceUses.Count(u => u.Kind == kind && u.Date >= weekStart && u.Date <= weekEnd);

        return new WeeklyGoals(
            weekStart,
            Planned(SessionKind.Lift), Done(SessionKind.Lift),
            Planned(SessionKind.Cardio), Done(SessionKind.Cardio),
            Planned(SessionKind.Mobility), Done(SessionKind.Mobility),
            Used(AllowanceKind.FlexibleMeal), plan.Allowances.FlexibleMeals,
            Used(AllowanceKind.Alcohol), plan.Allowances.AlcoholOccasions);
    }

    /// <summary>
    /// A completion belongs to a scheduled slot when kinds match and, for lifts, the A2S2 day matches.
    /// Cardio and mobility slots match on protocol/routine when both sides name one; an open slot
    /// ("C25K or LISS") or an untyped completion matches any of that kind.
    /// </summary>
    private static bool Matches(ScheduledSession session, SessionCompletion completion)
    {
        if (session.Kind != completion.Kind)
        {
            return false;
        }

        return session.Kind switch
        {
            SessionKind.Lift => session.LiftDay == completion.LiftDay,
            SessionKind.Cardio => session.CardioProtocolId is null
                || completion.CardioProtocolId is null
                || session.CardioProtocolId == completion.CardioProtocolId,
            SessionKind.Mobility => session.MobilityRoutineId is null
                || completion.MobilityRoutineId is null
                || session.MobilityRoutineId == completion.MobilityRoutineId,
            _ => false
        };
    }
}
