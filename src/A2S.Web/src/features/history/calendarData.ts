import type { WorkoutActivityDto, WorkoutHistoryDto } from './historyTypes';

export interface CalendarDay {
  date: Date;
  activity: WorkoutActivityDto | null;
  isToday: boolean;
  isFuture: boolean;
  /** In the first week, but before the day the program started */
  beforeStart: boolean;
}

export interface CalendarGrid {
  /** Every day in order, in whole Monday-to-Sunday weeks (so `days.length` is `weekCount * 7`) */
  days: CalendarDay[];
  weekCount: number;
  /** Where to print a month name: the first week, then each week a new month begins in */
  months: { week: number; label: string }[];
}

export const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());

/** The Monday on or before `d`. */
function mondayOf(d: Date): Date {
  const day = startOfDay(d);
  day.setDate(day.getDate() - ((day.getDay() + 6) % 7));
  return day;
}

/**
 * The training calendar as one grid: whole weeks from the week the program started to the
 * week it is planned to end (or the current week, if it has overrun), so the weeks still to
 * come are on the page too. Sessions are matched on the local calendar date.
 */
export function buildCalendarGrid(
  history: WorkoutHistoryDto | null | undefined,
  now = new Date()
): CalendarGrid {
  if (!history || !history.startedAt) return { days: [], weekCount: 0, months: [] };

  const started = startOfDay(new Date(history.startedAt));
  const today = startOfDay(now);
  const first = mondayOf(started);

  const plannedLast = new Date(first);
  plannedLast.setDate(plannedLast.getDate() + history.totalWeeks * 7 - 1);
  const thisSunday = mondayOf(today);
  thisSunday.setDate(thisSunday.getDate() + 6);
  const last = plannedLast > thisSunday ? plannedLast : thisSunday;

  const byDate = new Map<string, WorkoutActivityDto>();
  for (const activity of history.completedActivities) {
    byDate.set(new Date(activity.completedAt).toDateString(), activity);
  }

  const days: CalendarDay[] = [];
  for (const date = new Date(first); date <= last; date.setDate(date.getDate() + 1)) {
    const day = new Date(date);
    days.push({
      date: day,
      activity: byDate.get(day.toDateString()) ?? null,
      isToday: day.getTime() === today.getTime(),
      isFuture: day > today,
      beforeStart: day < started,
    });
  }

  // A week is labelled with the month its Sunday falls in, which is the week that month's 1st is in
  const weekCount = days.length / 7;
  const months: CalendarGrid['months'] = [];
  let previous = -1;
  for (let week = 0; week < weekCount; week++) {
    const month = days[week * 7 + 6].date.getMonth();
    if (month !== previous) months.push({ week, label: MONTHS[month] });
    previous = month;
  }

  return { days, weekCount, months };
}
