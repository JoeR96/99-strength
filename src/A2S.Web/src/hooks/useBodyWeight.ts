/** Weigh-ins and the trend. */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { bodyWeightApi } from '@/api/bodyWeight';
import type { LocalDate } from '@/lib/localDate';
import { habitKeys } from './useHabits';

export const bodyWeightKeys = {
  all: ['body-weight'] as const,
  trend: (from?: LocalDate, to?: LocalDate) => [...bodyWeightKeys.all, 'trend', from ?? null, to ?? null] as const,
};

export function useWeightTrend(from?: LocalDate, to?: LocalDate, enabled = true) {
  return useQuery({
    queryKey: bodyWeightKeys.trend(from, to),
    queryFn: () => bodyWeightApi.getTrend(from, to),
    enabled,
  });
}

/** Records or corrects a day's weigh-in; refreshes the trend and today's habits (the weigh-in habit). */
export function useRecordWeight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ date, kg }: { date: LocalDate; kg: number }) => bodyWeightApi.record(date, kg),
    onSuccess: (_entry, { date }) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: bodyWeightKeys.all }),
        queryClient.invalidateQueries({ queryKey: habitKeys.today(date) }),
      ]),
  });
}
