/**
 * Local dates for the plan, habits and weigh-ins.
 *
 * Every date the API speaks is a Europe/London calendar date as `yyyy-MM-dd`.
 * The client must agree with the server about which day "today" is, including
 * across the BST/GMT changes, so the current date is always derived through
 * `Intl.DateTimeFormat` with the London time zone — never from
 * `new Date().toISOString().slice(0, 10)`, which is the UTC date.
 */

export const LOCAL_TIME_ZONE = 'Europe/London';

const ymdFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: LOCAL_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** A `yyyy-MM-dd` local date string. */
export type LocalDate = string;

/** Formats an instant as the London calendar date it falls on. */
export function formatLocal(date: Date): LocalDate {
  const parts = ymdFormatter.formatToParts(date);
  const pick = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? '';
  return `${pick('year')}-${pick('month')}-${pick('day')}`;
}

/** Today's London calendar date. Pass `now` to evaluate another instant. */
export function todayLocal(now: Date = new Date()): LocalDate {
  return formatLocal(now);
}

function parse(date: LocalDate): { y: number; m: number; d: number } {
  const [y, m, d] = date.split('-').map(Number);
  return { y, m, d };
}

/** Days since the Unix epoch for a calendar date, ignoring time zones entirely. */
export function toEpochDays(date: LocalDate): number {
  const { y, m, d } = parse(date);
  return Math.round(Date.UTC(y, m - 1, d) / 86_400_000);
}

export function fromEpochDays(days: number): LocalDate {
  const dt = new Date(days * 86_400_000);
  const y = dt.getUTCFullYear();
  const m = String(dt.getUTCMonth() + 1).padStart(2, '0');
  const d = String(dt.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Calendar arithmetic on a `yyyy-MM-dd` string; DST never applies. */
export function addDays(date: LocalDate, days: number): LocalDate {
  return fromEpochDays(toEpochDays(date) + days);
}

/** Positive when `to` is after `from`. */
export function daysBetween(from: LocalDate, to: LocalDate): number {
  return toEpochDays(to) - toEpochDays(from);
}

export function compareLocalDates(a: LocalDate, b: LocalDate): number {
  return toEpochDays(a) - toEpochDays(b);
}

/** 0 = Monday … 6 = Sunday (ISO weekday, zero based). */
export function isoWeekday(date: LocalDate): number {
  // 1970-01-01 was a Thursday (ISO weekday 4 → index 3).
  return (((toEpochDays(date) + 3) % 7) + 7) % 7;
}

/** The Monday that starts the ISO week containing `date`. */
export function weekStartLocal(date: LocalDate): LocalDate {
  return addDays(date, -isoWeekday(date));
}

const dayLabelFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'UTC',
  weekday: 'short',
  day: 'numeric',
  month: 'short',
});

/** "Mon 9 Nov" for a `yyyy-MM-dd` string. */
export function formatDayLabel(date: LocalDate): string {
  const { y, m, d } = parse(date);
  return dayLabelFormatter.format(new Date(Date.UTC(y, m - 1, d))).replace(',', '');
}

const longLabelFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'UTC',
  day: 'numeric',
  month: 'short',
});

/** "3 Jan" for a `yyyy-MM-dd` string. */
export function formatShortDate(date: LocalDate): string {
  const { y, m, d } = parse(date);
  return longLabelFormatter.format(new Date(Date.UTC(y, m - 1, d)));
}
