import { describe, expect, it } from 'vitest';
import { formatWeekRange, isThisWeek, parseDateString, resolveWeek, shiftWeek, toDateString, weekStartOf } from './week';

describe('week helpers', () => {
  it('snaps any day to its Monday', () => {
    expect(toDateString(weekStartOf(new Date(2026, 10, 9)))).toBe('2026-11-09');
    expect(toDateString(weekStartOf(new Date(2026, 10, 11)))).toBe('2026-11-09');
    expect(toDateString(weekStartOf(new Date(2026, 10, 15)))).toBe('2026-11-09');
    expect(toDateString(weekStartOf(new Date(2026, 10, 16)))).toBe('2026-11-16');
  });

  it('resolves the week param, falling back to today', () => {
    const today = new Date(2026, 10, 12);
    expect(resolveWeek('2026-11-18', today)).toBe('2026-11-16');
    expect(resolveWeek(null, today)).toBe('2026-11-09');
    expect(resolveWeek('nonsense', today)).toBe('2026-11-09');
  });

  it('shifts whole weeks', () => {
    expect(shiftWeek('2026-11-09', 1)).toBe('2026-11-16');
    expect(shiftWeek('2026-11-09', -1)).toBe('2026-11-02');
    expect(shiftWeek('2026-12-28', 1)).toBe('2027-01-04');
  });

  it('parses only yyyy-MM-dd', () => {
    expect(parseDateString('2026-11-09')?.getDate()).toBe(9);
    expect(parseDateString('09/11/2026')).toBeNull();
    expect(parseDateString(undefined)).toBeNull();
  });

  it('formats the Monday to Sunday range', () => {
    expect(formatWeekRange('2026-11-09')).toBe('Mon 9 Nov – Sun 15 Nov');
  });

  it('knows this week', () => {
    expect(isThisWeek('2026-11-09', new Date(2026, 10, 13))).toBe(true);
    expect(isThisWeek('2026-11-02', new Date(2026, 10, 13))).toBe(false);
  });
});
