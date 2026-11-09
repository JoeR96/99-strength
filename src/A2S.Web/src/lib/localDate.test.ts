import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  todayLocal,
  formatLocal,
  addDays,
  weekStartLocal,
  formatDayLabel,
  compareLocalDates,
  daysBetween,
} from './localDate';

afterEach(() => {
  vi.useRealTimers();
});

describe('localDate', () => {
  describe('todayLocal across the 25 Oct 2026 clock change (BST → GMT)', () => {
    it('is already the 25th at 23:30Z on the 24th (London is BST, UTC+1)', () => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2026-10-24T23:30:00Z'));
      expect(todayLocal()).toBe('2026-10-25');
    });

    it('is still the 25th at 23:30Z on the 25th (London is back on GMT)', () => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2026-10-25T23:30:00Z'));
      expect(todayLocal()).toBe('2026-10-25');
    });

    it('rolls over at midnight local, not midnight UTC', () => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2026-10-25T23:59:59Z'));
      expect(todayLocal()).toBe('2026-10-25');
      vi.setSystemTime(new Date('2026-10-26T00:00:00Z'));
      expect(todayLocal()).toBe('2026-10-26');
    });
  });

  describe('todayLocal across the 28 Mar 2027 clock change (GMT → BST)', () => {
    it('is the 27th at 23:30Z on the 27th (still GMT)', () => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2027-03-27T23:30:00Z'));
      expect(todayLocal()).toBe('2027-03-27');
    });

    it('is already the 29th at 23:30Z on the 28th (BST, UTC+1)', () => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2027-03-28T23:30:00Z'));
      expect(todayLocal()).toBe('2027-03-29');
    });
  });

  it('formatLocal never uses the UTC date', () => {
    expect(formatLocal(new Date('2026-07-01T23:30:00Z'))).toBe('2026-07-02');
    expect(formatLocal(new Date('2026-01-01T23:30:00Z'))).toBe('2026-01-01');
  });

  it('addDays does plain calendar arithmetic on yyyy-MM-dd', () => {
    expect(addDays('2026-10-25', 1)).toBe('2026-10-26');
    expect(addDays('2026-10-25', -1)).toBe('2026-10-24');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2027-03-28', 1)).toBe('2027-03-29');
  });

  it('weekStartLocal returns the Monday of the week', () => {
    expect(weekStartLocal('2026-11-08')).toBe('2026-11-02'); // Sunday → previous Monday
    expect(weekStartLocal('2026-11-09')).toBe('2026-11-09'); // Monday
    expect(weekStartLocal('2026-11-11')).toBe('2026-11-09'); // Wednesday
  });

  it('formatDayLabel gives a short human label', () => {
    expect(formatDayLabel('2026-11-09')).toBe('Mon 9 Nov');
  });

  it('compareLocalDates and daysBetween', () => {
    expect(compareLocalDates('2026-11-09', '2026-11-10')).toBeLessThan(0);
    expect(compareLocalDates('2026-11-10', '2026-11-10')).toBe(0);
    expect(daysBetween('2026-11-09', '2026-11-16')).toBe(7);
    expect(daysBetween('2027-03-27', '2027-03-29')).toBe(2);
  });
});
