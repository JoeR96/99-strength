import { describe, it, expect } from 'vitest';
import { habitsTodayFixture, WALKS_ID, WATER_ID } from '@/mocks/fixtures/habits';
import { todayCardio } from '@/mocks/fixtures/plan';
import { SessionKind, SessionStatus } from '@/types/plan';
import { overlayHabitOps, overlayPlanOps, overlayTrendOps } from './overlay';
import type { OfflineOp } from './types';

const base = { id: 'x', createdAt: '2026-11-09T09:00:00.000Z', attempts: 0 };
const date = '2026-11-09';

describe('overlayHabitOps', () => {
  it('adds pending increments and applies pending sets, in order, for the same date only', () => {
    const data = habitsTodayFixture(date); // water 2, walks 1
    const ops: OfflineOp[] = [
      { ...base, id: 'a', kind: 'habit.increment', localDate: date, target: { habitId: WATER_ID }, payload: { delta: 1, clientTimestamp: '' } },
      { ...base, id: 'b', kind: 'habit.increment', localDate: date, target: { habitId: WATER_ID }, payload: { delta: 1, clientTimestamp: '' } },
      { ...base, id: 'c', kind: 'habit.set', localDate: date, target: { habitId: WALKS_ID }, payload: { value: 0, clientTimestamp: '' } },
      { ...base, id: 'd', kind: 'habit.increment', localDate: '2026-11-08', target: { habitId: WATER_ID }, payload: { delta: 1, clientTimestamp: '' } },
    ];

    const result = overlayHabitOps(data, ops);

    const water = result.habits.find((h) => h.id === WATER_ID)!;
    const walks = result.habits.find((h) => h.id === WALKS_ID)!;
    expect(water.value).toBe(4);
    expect(water.metMinimum).toBe(true);
    expect(walks.value).toBe(0);
    expect(walks.metMinimum).toBe(false);
  });

  it('returns the same object when nothing is pending', () => {
    const data = habitsTodayFixture(date);
    expect(overlayHabitOps(data, [])).toBe(data);
  });

  it('never goes below zero', () => {
    const data = habitsTodayFixture(date, { [WATER_ID]: 0 });
    const result = overlayHabitOps(data, [
      { ...base, kind: 'habit.increment', localDate: date, target: { habitId: WATER_ID }, payload: { delta: -1, clientTimestamp: '' } },
    ]);
    expect(result.habits.find((h) => h.id === WATER_ID)!.value).toBe(0);
  });
});

describe('overlayPlanOps', () => {
  it('marks the matching session Done while its completion is queued', () => {
    const today = todayCardio(date);
    const cardio = today.sessions.find((s) => s.kind === SessionKind.Cardio)!;
    const result = overlayPlanOps(today, [
      {
        ...base,
        kind: 'plan.complete',
        localDate: date,
        target: { planId: 'plan-1' },
        payload: { date, kind: SessionKind.Cardio, cardioProtocolId: cardio.cardioProtocolId, liftDay: null, mobilityRoutineId: null },
      },
    ])!;

    expect(result.sessions.find((s) => s.kind === SessionKind.Cardio)!.status).toBe(SessionStatus.Done);
    expect(result.sessions.find((s) => s.kind === SessionKind.Mobility)!.status).toBe(cardio.status);
  });

  it('passes null through', () => {
    expect(overlayPlanOps(null, [])).toBeNull();
  });
});

describe('overlayTrendOps', () => {
  it('replaces the day’s kg or adds the point', () => {
    const trend = { points: [{ date: '2026-11-08', kg: 95, movingAverage7: null, ewma: 95 }], pace: null };
    const result = overlayTrendOps(trend, [
      { ...base, kind: 'bodyweight.put', localDate: '2026-11-08', target: {}, payload: { kg: 94.8 } },
      { ...base, id: 'y', kind: 'bodyweight.put', localDate: '2026-11-09', target: {}, payload: { kg: 94.6 } },
    ]);
    expect(result.points.map((p) => [p.date, p.kg])).toEqual([
      ['2026-11-08', 94.8],
      ['2026-11-09', 94.6],
    ]);
  });
});
