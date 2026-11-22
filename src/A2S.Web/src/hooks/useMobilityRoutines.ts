import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import { todayLocal } from '@/lib/localDate';
import type { DrillFeedbackRequest } from '@/types/mobility';
import type { CreateMobilityRoutineRequest, MobilityDrillDto, MobilityRoutineDto, UpdateMobilityRoutineRequest } from '@/types/mobility';

export const mobilityKeys = {
  all: ['mobility-routines'] as const,
};

export function useMobilityRoutines() {
  return useQuery({
    queryKey: mobilityKeys.all,
    queryFn: () => mobilityRoutinesApi.list(),
    staleTime: 5 * 60 * 1000,
  });
}

/** One routine by id from the cached list; `undefined` while loading or when it doesn't exist. */
export function useMobilityRoutine(id: string | undefined) {
  const query = useMobilityRoutines();
  return { ...query, routine: id ? query.data?.find((r) => r.id === id) : undefined };
}

export function useCreateMobilityRoutine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMobilityRoutineRequest) => mobilityRoutinesApi.create(request),
    onSuccess: (created) => {
      queryClient.setQueryData<MobilityRoutineDto[]>(mobilityKeys.all, (old) => [...(old ?? []), created]);
    },
  });
}

export function useReplaceDrills() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, drills }: { id: string; drills: MobilityDrillDto[] }) => mobilityRoutinesApi.replaceDrills(id, drills),
    onSuccess: (updated) => {
      queryClient.setQueryData<MobilityRoutineDto[]>(mobilityKeys.all, (old) =>
        (old ?? []).map((r) => (r.id === updated.id ? updated : r))
      );
    },
  });
}

export function useUpdateMobilityRoutine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateMobilityRoutineRequest }) => mobilityRoutinesApi.update(id, request),
    onSuccess: (updated) => {
      queryClient.setQueryData<MobilityRoutineDto[]>(mobilityKeys.all, (old) =>
        (old ?? []).map((r) => (r.id === updated.id ? updated : r))
      );
    },
  });
}

export function useDeleteMobilityRoutine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => mobilityRoutinesApi.remove(id),
    onSuccess: (_, id) => {
      queryClient.setQueryData<MobilityRoutineDto[]>(mobilityKeys.all, (old) => (old ?? []).filter((r) => r.id !== id));
    },
  });
}

function replaceInCache(queryClient: QueryClient, updated: MobilityRoutineDto) {
  queryClient.setQueryData<MobilityRoutineDto[]>(mobilityKeys.all, (old) => (old ?? []).map((r) => (r.id === updated.id ? updated : r)));
}

/**
 * Records how each drill felt after a home routine. Offline it is queued as a `mobility.feedback` op; the
 * request's `sessionKey` (made when the player opened) keeps a replay to one evaluation on the server.
 */
export function useRecordDrillFeedback() {
  const queryClient = useQueryClient();
  return useOfflineMutation({
    run: ({ id, request }: { id: string; request: DrillFeedbackRequest; label?: string }, key) => mobilityRoutinesApi.recordFeedback(id, request, key),
    toOp: ({ id, request, label }) => ({
      kind: 'mobility.feedback' as const,
      localDate: todayLocal(),
      label,
      target: { routineId: id },
      payload: request,
    }),
    onSuccess: (outcome) => {
      if (!outcome.queued) replaceInCache(queryClient, outcome.data.routine);
    },
  });
}

export function useRespondToDrillSuggestion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, drillName, accept }: { id: string; drillName: string; accept: boolean }) =>
      mobilityRoutinesApi.respondToSuggestion(id, drillName, accept),
    onSuccess: (updated) => replaceInCache(queryClient, updated),
  });
}

export function useUndoDrillProgression() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => mobilityRoutinesApi.undoProgression(id),
    onSuccess: (updated) => replaceInCache(queryClient, updated),
  });
}

export function useApplyHoldIncrease() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, seconds }: { id: string; seconds: number }) => mobilityRoutinesApi.applyHoldIncrease(id, seconds),
    onSuccess: (updated) => replaceInCache(queryClient, updated),
  });
}
