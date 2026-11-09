/**
 * Week helpers for the meals pages. Weeks run Monday to Sunday and are addressed by their
 * Monday as a `yyyy-MM-dd` string, matching the API.
 */

const DAY_MS = 24 * 60 * 60 * 1000;

/** `yyyy-MM-dd` from a local date (no timezone shift). */
export function toDateString(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Parses `yyyy-MM-dd` as a local date. Returns null for anything else. */
export function parseDateString(value: string | null | undefined): Date | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const [y, m, d] = value.split('-').map(Number);
  const date = new Date(y, m - 1, d);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** The Monday of the week `date` falls in. */
export function weekStartOf(date: Date): Date {
  const monday = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const offset = (monday.getDay() + 6) % 7;
  monday.setDate(monday.getDate() - offset);
  return monday;
}

/** The week to show: the `week` query param snapped to its Monday, or this week. */
export function resolveWeek(param: string | null | undefined, today: Date = new Date()): string {
  const parsed = parseDateString(param);
  return toDateString(weekStartOf(parsed ?? today));
}

export function shiftWeek(week: string, weeks: number): string {
  const start = parseDateString(week) ?? weekStartOf(new Date());
  return toDateString(new Date(start.getTime() + weeks * 7 * DAY_MS));
}

/** "Mon 9 Nov – Sun 15 Nov" for the header. */
export function formatWeekRange(week: string): string {
  const start = parseDateString(week);
  if (!start) return week;
  const end = new Date(start.getTime() + 6 * DAY_MS);
  const fmt = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
  return `${fmt.format(start)} – ${fmt.format(end)}`;
}

export function isThisWeek(week: string, today: Date = new Date()): boolean {
  return week === toDateString(weekStartOf(today));
}
