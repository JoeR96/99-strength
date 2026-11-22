import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
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
