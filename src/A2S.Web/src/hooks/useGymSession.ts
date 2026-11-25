/** Gym mode's server state: finishing a session (queued offline), retrying its Hevy post, cardio protocols. */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { cardioProtocolsApi, gymSessionsApi } from '@/api/gymSessions';
import { plansApi } from '@/api/plans';
import { formatLocal } from '@/lib/localDate';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import { planKeys } from '@/hooks/usePlan';
import { workoutKeys } from '@/hooks/useWorkouts';
import type { FinishGymSessionRequest } from '@/types/gym';
import type { ScheduledSessionDto } from '@/types/plan';

export const cardioProtocolKeys = { all: ['cardio-protocols'] as const };

export function useCardioProtocols(enabled = true) {
  return useQuery({
    queryKey: cardioProtocolKeys.all,
    queryFn: () => cardioProtocolsApi.list(),
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

/**
 * Finishes a gym session. Offline (or when the request dies on the wire) the finish is queued as a
 * `gym.finish` op carrying the client session id, so however many times it is replayed the server
 * completes the session once.
 */
export function useFinishGymSession(label?: string) {
  const queryClient = useQueryClient();
  return useOfflineMutation({
    run: (request: FinishGymSessionRequest, key) => gymSessionsApi.finish(request, key),
    toOp: (request: FinishGymSessionRequest) => ({
      kind: 'gym.finish' as const,
      localDate: formatLocal(new Date(request.startedAt)),
      label,
      target: { clientSessionId: request.clientSessionId },
      payload: request,
    }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: workoutKeys.all });
      void queryClient.invalidateQueries({ queryKey: planKeys.all });
    },
  });
}

/** "Not in Hevy yet" → Retry. */
export function useRetryHevyPost() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (clientSessionId: string) => gymSessionsApi.retryHevy(clientSessionId),
    onSuccess: (status) => {
      if (status.status === 'Posted' || status.status === 'UpdatePending') toast.success('In Hevy now');
      else toast.error(status.lastError ? `Still not in Hevy: ${status.lastError}` : 'Still not in Hevy; it will keep trying');
      void queryClient.invalidateQueries({ queryKey: planKeys.all });
    },
    onError: () => toast.error('Could not reach the server; it will keep trying'),
  });
}

/** Saves the whole schedule (a lift slot's warm-up or finisher changed). */
export function useUpdateSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ planId, schedule }: { planId: string; schedule: ScheduledSessionDto[] }) => plansApi.updateSchedule(planId, schedule),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: planKeys.all }),
  });
}
