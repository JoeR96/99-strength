/** Training-plan queries and mutations. Every date is a `yyyy-MM-dd` local date. */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { plansApi } from '@/api/plans';
import type { LocalDate } from '@/lib/localDate';
import { useOfflineSync } from '@/offline/context';
import { overlayPlanOps } from '@/offline/overlay';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import { SessionStatus, type AllowanceKind, type CompleteSessionRequest, type TodayDto } from '@/types/plan';

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
  const sync = useOfflineSync();
  return useQuery({
    queryKey: planKeys.today(date),
    queryFn: async () => overlayPlanOps(await plansApi.getToday(date), await sync.queue.all()),
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

export interface CompleteSessionVariables {
  planId: string;
  request: CompleteSessionRequest;
  /** The session's label ("Norwegian 4×4") for a toast should the queued op be refused. */
  label?: string;
}

/** Marks the matching session Done in the cached day while the completion waits in the queue. */
function markDoneLocally(today: TodayDto | null | undefined, request: CompleteSessionRequest): TodayDto | null | undefined {
  if (!today) return today;
  return {
    ...today,
    sessions: today.sessions.map((s) =>
      s.kind === request.kind &&
      (request.liftDay ?? null) === s.liftDay &&
      (request.cardioProtocolId ?? null) === s.cardioProtocolId &&
      (request.mobilityRoutineId ?? null) === s.mobilityRoutineId
        ? { ...s, status: SessionStatus.Done }
        : s
    ),
  };
}

/** Marks a cardio or mobility session (or a manual lift) done; queued when offline. */
export function useCompleteSession() {
  const queryClient = useQueryClient();
  return useOfflineMutation({
    run: ({ planId, request }: CompleteSessionVariables) => plansApi.completeSession(planId, request),
    toOp: ({ planId, request, label }) => ({
      kind: 'plan.complete',
      localDate: request.date,
      target: { planId },
      payload: request,
      label,
    }),
    onSuccess: (outcome, { request }) => {
      if (outcome.queued) {
        queryClient.setQueryData<TodayDto | null>(planKeys.today(request.date), (old) => markDoneLocally(old, request));
        return;
      }
      return invalidatePlanViews(queryClient);
    },
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
    mutationFn: async ({ planId, date, kind, remove }: { planId: string; date: LocalDate; kind: AllowanceKind; remove?: boolean }) => {
      if (remove) {
        await plansApi.removeAllowanceUse(planId, date, kind);
      } else {
        await plansApi.useAllowance(planId, { date, kind });
      }
    },
    onSuccess: () => invalidatePlanViews(queryClient),
  });
}
