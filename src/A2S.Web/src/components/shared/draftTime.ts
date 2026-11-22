/** "today at 19:30" or "Wed 18 Nov at 07:05", in Europe/London time. */

const timeFormat = new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/London', hour: '2-digit', minute: '2-digit' });
const dayFormat = new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/London', weekday: 'short', day: 'numeric', month: 'short' });

export function describeSavedAt(savedAt: string, now: Date = new Date()): string {
  const at = new Date(savedAt);
  if (Number.isNaN(at.getTime())) return 'earlier';
  const sameDay = dayFormat.format(at) === dayFormat.format(now);
  return sameDay ? `today at ${timeFormat.format(at)}` : `${dayFormat.format(at)} at ${timeFormat.format(at)}`;
}
