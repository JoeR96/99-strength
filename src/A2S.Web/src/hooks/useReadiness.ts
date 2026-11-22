/** Daily readiness (Today's one-tap Green / Yellow / Red). */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { readinessApi } from '@/api/readiness';
import type { LocalDate } from '@/lib/localDate';
import type { ReadinessColour, ReadinessDto } from '@/types/readiness';

export const readinessKeys = {
  all: ['readiness'] as const,
  day: (date: LocalDate) => [...readinessKeys.all, date] as const,
};

export function useReadiness(date: LocalDate, enabled = true) {
  return useQuery({ queryKey: readinessKeys.day(date), queryFn: () => readinessApi.get(date), enabled });
}

function useReadinessMutation<T>(fn: (args: T) => Promise<ReadinessDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (dto) => queryClient.setQueryData(readinessKeys.day(dto.date), dto),
  });
}

export function useSetReadiness() {
  return useReadinessMutation(({ date, colour }: { date: LocalDate; colour: ReadinessColour }) => readinessApi.set(date, colour));
}

export function useSetReadinessFinisher() {
  return useReadinessMutation(({ date, keep }: { date: LocalDate; keep: boolean }) => readinessApi.setFinisher(date, keep));
}
