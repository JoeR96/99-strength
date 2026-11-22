import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cardioTracksApi } from '@/api/cardioTracks';
import type { CardioTrackDto, LogTrackSessionRequest, UpdateCardioTrackRequest } from '@/types/cardioTracks';

export const cardioTrackKeys = {
  all: ['cardio-tracks'] as const,
  list: () => [...cardioTrackKeys.all, 'list'] as const,
  templates: () => [...cardioTrackKeys.all, 'templates'] as const,
  today: () => [...cardioTrackKeys.all, 'today'] as const,
  detail: (id: string) => [...cardioTrackKeys.all, 'detail', id] as const,
};

export function useCardioTracks() {
  return useQuery({ queryKey: cardioTrackKeys.list(), queryFn: () => cardioTracksApi.list() });
}

export function useCardioTrackTemplates() {
  return useQuery({ queryKey: cardioTrackKeys.templates(), queryFn: () => cardioTracksApi.templates(), staleTime: Infinity });
}

export function useTodayCardio() {
  return useQuery({ queryKey: cardioTrackKeys.today(), queryFn: () => cardioTracksApi.today(), retry: false });
}

export function useCardioTrack(id: string | undefined) {
  return useQuery({
    queryKey: cardioTrackKeys.detail(id ?? ''),
    queryFn: () => cardioTracksApi.get(id!),
    enabled: Boolean(id),
  });
}

/** Every track mutation refreshes the list, Today and the details (prescriptions move together). */
function useTrackMutation<TArgs, TResult>(fn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: cardioTrackKeys.all }),
  });
}

export function useStartCardioTrack() {
  return useTrackMutation(({ templateKey, name }: { templateKey: string; name?: string }) => cardioTracksApi.create(templateKey, name));
}

export function useUpdateCardioTrack() {
  return useTrackMutation(({ id, request }: { id: string; request: UpdateCardioTrackRequest }) => cardioTracksApi.update(id, request));
}

export function useLogTrackSession() {
  return useTrackMutation(({ id, request }: { id: string; request: LogTrackSessionRequest }) => cardioTracksApi.logSession(id, request));
}

export function useDecideSuggestion() {
  return useTrackMutation(({ id, accept }: { id: string; accept: boolean }) => cardioTracksApi.decide(id, accept));
}

export function useUndoTrack() {
  return useTrackMutation((id: string) => cardioTracksApi.undo(id));
}

export function useSetTrackPaused() {
  return useTrackMutation(({ track, paused }: { track: CardioTrackDto; paused: boolean }) =>
    paused ? cardioTracksApi.pause(track.id) : cardioTracksApi.resume(track.id)
  );
}

export function useRecordBenchmark() {
  return useTrackMutation(({ id, value, notes }: { id: string; value: number; notes?: string }) =>
    cardioTracksApi.recordBenchmark(id, value, notes)
  );
}
