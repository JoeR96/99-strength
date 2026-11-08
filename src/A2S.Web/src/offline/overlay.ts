/**
 * Pending ops laid over freshly fetched data. While the connection is down the
 * service worker still answers GETs from its cache, and those answers predate the
 * taps waiting in the queue; without this a refetch would snap a counter back to
 * the cached value. Each query applies the ops for its date on top of what the
 * server (or the cache) returned, so the screen always shows server + pending.
 */

import type { HabitsTodayDto } from '@/types/habits';
import { SessionStatus, type TodayDto } from '@/types/plan';
import type { WeightTrendDto } from '@/types/bodyWeight';
import type { OfflineOp } from './types';

export function overlayHabitOps(data: HabitsTodayDto, ops: OfflineOp[]): HabitsTodayDto {
  const relevant = ops.filter(
    (op): op is Extract<OfflineOp, { kind: 'habit.increment' | 'habit.set' }> =>
      op.localDate === data.date && (op.kind === 'habit.increment' || op.kind === 'habit.set')
  );
  if (relevant.length === 0) return data;

  const values = new Map(data.habits.map((h) => [h.id, h.value]));
  for (const op of relevant) {
    const current = values.get(op.target.habitId);
    if (current === undefined) continue;
    values.set(
      op.target.habitId,
      op.kind === 'habit.set' ? op.payload.value : Math.max(0, current + op.payload.delta)
    );
  }

  return {
    ...data,
    habits: data.habits.map((h) => {
      const value = values.get(h.id) ?? h.value;
      return value === h.value ? h : { ...h, value, metMinimum: value >= h.minimum, metTarget: value >= h.target };
    }),
  };
}

export function overlayPlanOps(today: TodayDto | null, ops: OfflineOp[]): TodayDto | null {
  if (!today) return today;
  const relevant = ops.filter(
    (op): op is Extract<OfflineOp, { kind: 'plan.complete' }> => op.kind === 'plan.complete' && op.localDate === today.date
  );
  if (relevant.length === 0) return today;

  return {
    ...today,
    sessions: today.sessions.map((session) => {
      const done = relevant.some(
        (op) =>
          op.payload.kind === session.kind &&
          (op.payload.liftDay ?? null) === session.liftDay &&
          (op.payload.cardioProtocolId ?? null) === session.cardioProtocolId &&
          (op.payload.mobilityRoutineId ?? null) === session.mobilityRoutineId
      );
      return done && session.status !== SessionStatus.Done ? { ...session, status: SessionStatus.Done } : session;
    }),
  };
}

export function overlayTrendOps(trend: WeightTrendDto, ops: OfflineOp[]): WeightTrendDto {
  const relevant = ops.filter((op): op is Extract<OfflineOp, { kind: 'bodyweight.put' }> => op.kind === 'bodyweight.put');
  if (relevant.length === 0) return trend;

  const points = [...trend.points];
  for (const op of relevant) {
    const index = points.findIndex((p) => p.date === op.localDate);
    if (index >= 0) {
      points[index] = { ...points[index], kg: op.payload.kg };
    } else {
      points.push({ date: op.localDate, kg: op.payload.kg, movingAverage7: null, ewma: op.payload.kg });
    }
  }
  points.sort((a, b) => (a.date < b.date ? -1 : a.date > b.date ? 1 : 0));
  return { ...trend, points };
}
