import { describe, expect, it } from 'vitest';
import { buildCalendarGrid } from './calendarData';
import type { WorkoutActivityDto, WorkoutHistoryDto } from './historyTypes';

const session = (iso: string, weekNumber: number, dayNumber: number): WorkoutActivityDto => ({
  day: `Day${dayNumber}`,
  dayNumber,
  weekNumber,
  blockNumber: 1,
  completedAt: iso,
  isDeloadWeek: false,
  performances: [],
});

// A three-week program that started on Wednesday 2 September 2026
const history = {
  startedAt: new Date(2026, 8, 2, 7, 30).toISOString(),
  totalWeeks: 3,
  completedActivities: [
    session(new Date(2026, 8, 2, 7, 30).toISOString(), 1, 1),
    session(new Date(2026, 8, 8, 7, 30).toISOString(), 2, 1),
  ],
} as WorkoutHistoryDto;

const now = new Date(2026, 8, 9, 12, 0); // Wednesday of week 2

describe('buildCalendarGrid', () => {
  it('is empty without a started program', () => {
    expect(buildCalendarGrid(null, now).days).toEqual([]);
    expect(buildCalendarGrid({ ...history, startedAt: null }, now).days).toEqual([]);
  });

  it('runs in whole Monday-to-Sunday weeks from the start week to the end of the program', () => {
    const grid = buildCalendarGrid(history, now);
    expect(grid.weekCount).toBe(3);
    expect(grid.days).toHaveLength(21);
    expect(grid.days[0].date).toEqual(new Date(2026, 7, 31)); // Monday before the start
    expect(grid.days[20].date).toEqual(new Date(2026, 8, 20)); // Sunday of week 3
  });

  it('keeps going to the current week when the program has overrun', () => {
    const late = new Date(2026, 9, 1, 12, 0); // Thursday, two weeks after the planned end
    const grid = buildCalendarGrid(history, late);
    expect(grid.weekCount).toBe(5);
    expect(grid.days[grid.days.length - 1].date).toEqual(new Date(2026, 9, 4));
  });

  it('puts each session on its local calendar day', () => {
    const grid = buildCalendarGrid(history, now);
    const withSessions = grid.days.filter((d) => d.activity).map((d) => d.date.getDate());
    expect(withSessions).toEqual([2, 8]);
  });

  it('marks today, days still to come and days before the program started', () => {
    const grid = buildCalendarGrid(history, now);
    const byDate = (day: number) => grid.days.find((d) => d.date.getDate() === day && d.date.getMonth() === 8)!;
    expect(byDate(9)).toMatchObject({ isToday: true, isFuture: false });
    expect(byDate(10)).toMatchObject({ isToday: false, isFuture: true });
    expect(byDate(8)).toMatchObject({ isToday: false, isFuture: false, beforeStart: false });
    expect(grid.days[0].beforeStart).toBe(true);
  });

  it('labels the first week, then each week a new month begins in', () => {
    const grid = buildCalendarGrid({ ...history, totalWeeks: 6 }, now);
    // Week 0 runs 31 Aug to 6 Sep, week 4 runs 28 Sep to 4 Oct
    expect(grid.months).toEqual([
      { week: 0, label: 'Sep' },
      { week: 4, label: 'Oct' },
    ]);
  });
});
