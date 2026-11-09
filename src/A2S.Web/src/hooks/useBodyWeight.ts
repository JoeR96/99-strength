/** Weigh-ins and the trend. */

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bodyWeightApi } from '@/api/bodyWeight';
import type { LocalDate } from '@/lib/localDate';
import { useOfflineSync } from '@/offline/context';
import { overlayTrendOps } from '@/offline/overlay';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import type { WeightTrendDto } from '@/types/bodyWeight';
import { habitKeys } from './useHabits';

export const bodyWeightKeys = {
  all: ['body-weight'] as const,
  trend: (from?: LocalDate, to?: LocalDate) => [...bodyWeightKeys.all, 'trend', from ?? null, to ?? null] as const,
};

export function useWeightTrend(from?: LocalDate, to?: LocalDate, enabled = true) {
  const sync = useOfflineSync();
  return useQuery({
    queryKey: bodyWeightKeys.trend(from, to),
    queryFn: async () => overlayTrendOps(await bodyWeightApi.getTrend(from, to), await sync.queue.all()),
    enabled,
  });
}

/** Puts the day's kg into a cached trend so the field shows it while the weigh-in waits in the queue. */
function upsertPoint(trend: WeightTrendDto | undefined, date: LocalDate, kg: number): WeightTrendDto | undefined {
  if (!trend) return trend;
  const exists = trend.points.some((p) => p.date === date);
  const points = exists
    ? trend.points.map((p) => (p.date === date ? { ...p, kg } : p))
    : [...trend.points, { date, kg, movingAverage7: null, ewma: kg }].sort((a, b) => a.date.localeCompare(b.date));
  return { ...trend, points };
}

/**
 * Records or corrects a day's weigh-in; refreshes the trend and today's habits (the weigh-in habit).
 * Offline the entry is queued and every cached trend shows the new kg until it replays.
 */
export function useRecordWeight() {
  const queryClient = useQueryClient();
  return useOfflineMutation({
    run: ({ date, kg }: { date: LocalDate; kg: number }) => bodyWeightApi.record(date, kg),
    toOp: ({ date, kg }) => ({ kind: 'bodyweight.put', localDate: date, target: {}, payload: { kg } }),
    onSuccess: (outcome, { date, kg }) => {
      if (outcome.queued) {
        queryClient.setQueriesData<WeightTrendDto>({ queryKey: [...bodyWeightKeys.all, 'trend'] }, (old) =>
          upsertPoint(old, date, kg)
        );
        return;
      }
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: bodyWeightKeys.all }),
        queryClient.invalidateQueries({ queryKey: habitKeys.today(date) }),
      ]);
    },
  });
}
