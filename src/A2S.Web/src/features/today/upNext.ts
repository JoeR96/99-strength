/**
 * Pure helpers for looking ahead on Today: which upcoming day is "Up next", how to head a day
 * ("Tomorrow · Tue 24 Nov"), the `?date=` parameter, and the one-line lift summary.
 */

import { addDays, formatDayLabel, type LocalDate } from '@/lib/localDate';
import {
  SessionKind,
  SessionStatus,
  type PlanDayDto,
  type TodaySessionDto,
  type UpcomingDayDto,
  type UpcomingLiftDto,
} from '@/types/plan';

/** Sessions there is still something to do for (not rest, done or paused). */
export function toDo(sessions: TodaySessionDto[]): TodaySessionDto[] {
  return sessions.filter(
    (s) =>
      s.kind !== SessionKind.Rest &&
      s.status !== SessionStatus.Done &&
      s.status !== SessionStatus.Paused
  );
}

/** A rest day, an empty day, or a day where every session is done: time to show Up next. */
export function isAllDoneOrRest(sessions: TodaySessionDto[]): boolean {
  return sessions.every((s) => s.kind === SessionKind.Rest || s.status === SessionStatus.Done);
}

/** The first day in `days` with a session still to do, and those sessions; null when there is none. */
export function pickUpNext(
  days: (PlanDayDto | UpcomingDayDto)[] | null | undefined
): { date: LocalDate; sessions: TodaySessionDto[]; lifts: UpcomingLiftDto[] } | null {
  for (const day of days ?? []) {
    const sessions = toDo(day.sessions);
    if (sessions.length > 0)
      return { date: day.date, sessions, lifts: 'lifts' in day ? day.lifts : [] };
  }
  return null;
}

/** "Tomorrow · Tue 24 Nov", "Yesterday · …", "Today · …", or just "Thu 26 Nov". */
export function dayHeading(date: LocalDate, today: LocalDate): string {
  const label = formatDayLabel(date);
  if (date === today) return `Today · ${label}`;
  if (date === addDays(today, 1)) return `Tomorrow · ${label}`;
  if (date === addDays(today, -1)) return `Yesterday · ${label}`;
  return label;
}

/** A valid `yyyy-MM-dd` from the URL, or null. */
export function parseDateParam(value: string | null): LocalDate | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const [y, m, d] = value.split('-').map(Number);
  const date = new Date(Date.UTC(y, m - 1, d));
  return formatLocalUtc(date) === value ? value : null;
}

function formatLocalUtc(date: Date): string {
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
}

function formatWeight(weight: number): string {
  return Number.isInteger(weight) ? String(weight) : weight.toFixed(1);
}

/** "2 exercises · Bench Press (Barbell) 5×5 @ 75 kg": the main lift (MainLift first, else the first) and its top set. */
export function liftSummary(lift: UpcomingLiftDto): string | null {
  const exercises = lift.exercises.filter((e) => e.sets.length > 0);
  if (exercises.length === 0) return null;
  const main = exercises.find((e) => e.category === 'MainLift') ?? exercises[0];
  const top = main.sets.reduce((best, s) => (s.weightKg > best.weightKg ? s : best));
  const count = exercises.length === 1 ? '1 exercise' : `${exercises.length} exercises`;
  return `${count} · ${main.name} ${main.sets.length}×${top.reps} @ ${formatWeight(top.weightKg)} kg`;
}
