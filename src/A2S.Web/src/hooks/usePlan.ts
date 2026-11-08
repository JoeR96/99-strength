/** Training-plan queries and mutations. Every date is a `yyyy-MM-dd` local date. */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { plansApi } from '@/api/plans';
import type { LocalDate } from '@/lib/localDate';
import type { AllowanceKind, CompleteSessionRequest } from '@/types/plan';

export const planKeys = {
  all: ['plans'] as const,
  active: () => [...planKeys.all, 'active'] as const,
  today: (date: LocalDate) => [...planKeys.all, 'today', date] as const,
  weeklyGoals: (date: LocalDate) => [...planKeys.all, 'weekly-goals', date] as const,
  calendar: (year: number, month: number) => [...planKeys.all, 'calendar', year, month] as const,
};

/** The active plan; `null` on first run. */
export function useActivePlan() {
  return useQuery({
    queryKey: planKeys.active(),
    queryFn: () => plansApi.getActive(),
    staleTime: 5 * 60 * 1000,
  });
}

/** Sessions, phase and week for a date; `null` when there is no plan. */
export function useToday(date: LocalDate) {
  return useQuery({
    queryKey: planKeys.today(date),
    queryFn: () => plansApi.getToday(date),
    staleTime: 60 * 1000,
  });
}

export function useWeeklyGoals(date: LocalDate, enabled = true) {
  return useQuery({
    queryKey: planKeys.weeklyGoals(date),
    queryFn: () => plansApi.getWeeklyGoals(date),
    enabled,
  });
}

export function usePlanCalendar(year: number, month: number, enabled = true) {
  return useQuery({
    queryKey: planKeys.calendar(year, month),
    queryFn: () => plansApi.getCalendar(year, month),
    enabled,
  });
}

function invalidatePlanViews(queryClient: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'today'] }),
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'weekly-goals'] }),
    queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'calendar'] }),
    queryClient.invalidateQueries({ queryKey: planKeys.active() }),
  ]);
}

/** Marks a cardio or mobility session (or a manual lift) done. */
export function useCompleteSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, request }: { planId: string; request: CompleteSessionRequest }) =>
      plansApi.completeSession(planId, request),
    onSuccess: () => invalidatePlanViews(queryClient),
  });
}

export function useUndoSessionCompletion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, completionId }: { planId: string; completionId: string }) =>
      plansApi.undoCompletion(planId, completionId),
    onSuccess: () => invalidatePlanViews(queryClient),
  });
}

/** Uses or takes back one weekly allowance (flexible meal, alcohol occasion). */
export function useAllowance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, date, kind, remove }: { planId: string; date: LocalDate; kind: AllowanceKind; remove?: boolean }) =>
      remove ? plansApi.removeAllowanceUse(planId, date, kind) : plansApi.useAllowance(planId, { date, kind }),
    onSuccess: () => invalidatePlanViews(queryClient),
  });
}
