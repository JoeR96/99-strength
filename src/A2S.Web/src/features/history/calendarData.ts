import type { WorkoutActivityDto, WorkoutHistoryDto } from './WorkoutHistoryComponents';

export interface CalendarMonth {
  month: number;
  year: number;
  days: { date: Date; activity: WorkoutActivityDto | null }[];
}

/**
 * Month grids for the history calendar, from the program start to `now`.
 * Leading cells before the 1st are padded with `new Date(0)` (rendered empty).
 * Activities are matched on the local calendar date.
 */
export function buildCalendarMonths(
  history: WorkoutHistoryDto | null | undefined,
  now = new Date()
): CalendarMonth[] {
  if (!history || !history.startedAt) return [];

  const startDate = new Date(history.startedAt);
  const months: CalendarMonth[] = [];

  const activityMap = new Map<string, WorkoutActivityDto>();
  history.completedActivities.forEach((activity) => {
    activityMap.set(new Date(activity.completedAt).toDateString(), activity);
  });

  let currentMonth = new Date(startDate.getFullYear(), startDate.getMonth(), 1);
  while (currentMonth <= now) {
    const month = currentMonth.getMonth();
    const year = currentMonth.getFullYear();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const firstDayOfWeek = new Date(year, month, 1).getDay();

    const days: CalendarMonth['days'] = [];
    for (let i = 0; i < firstDayOfWeek; i++) {
      days.push({ date: new Date(0), activity: null });
    }
    for (let day = 1; day <= daysInMonth; day++) {
      const date = new Date(year, month, day);
      days.push({ date, activity: activityMap.get(date.toDateString()) || null });
    }

    months.push({ month, year, days });
    currentMonth = new Date(year, month + 1, 1);
  }

  return months;
}
