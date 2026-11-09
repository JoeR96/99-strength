/** Weekly hard sets, cap warnings and the time estimate for one lifting day. */

import { useQuery } from '@tanstack/react-query';
import { budgetApi } from '@/api/budget';

export const budgetKeys = {
  all: ['budget'] as const,
  day: (workoutId: string, day: number, week?: number) => [...budgetKeys.all, workoutId, day, week ?? null] as const,
};

export function useDayBudget(workoutId: string | null | undefined, day: number, week?: number) {
  return useQuery({
    queryKey: budgetKeys.day(workoutId ?? '', day, week),
    queryFn: () => budgetApi.getDayBudget(workoutId!, day, week),
    enabled: Boolean(workoutId) && day >= 1 && day <= 6,
    staleTime: 60 * 1000,
  });
}
