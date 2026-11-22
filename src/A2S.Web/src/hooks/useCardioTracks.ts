import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cardioTracksApi } from '@/api/cardioTracks';
import { useOfflineSync } from '@/offline/context';
import { overlayCardioOps } from '@/offline/overlay';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import { todayLocal } from '@/lib/localDate';
import type { CardioTrackDto, LogTrackSessionRequest, TodayCardioDto, UpdateCardioTrackRequest } from '@/types/cardioTracks';

export const cardioTrackKeys = {
  all: ['cardio-tracks'] as const,
  list: () => [...cardioTrackKeys.all, 'list'] as const,
  templates: () => [...cardioTrackKeys.all, 'templates'] as const,
  today: () => [...cardioTrackKeys.all, 'today'] as const,
  detail: (id: string) => [...cardioTrackKeys.all, 'detail', id] as const,
};

/** Track lists carry the logs still queued on this phone (a "Waiting to sync" badge per track). */
export function useCardioTracks(enabled = true) {
  const sync = useOfflineSync();
  return useQuery({
    queryKey: cardioTrackKeys.list(),
    queryFn: async () => overlayCardioOps({ tracks: await cardioTracksApi.list() }, await sync.queue.all()).tracks,
    enabled,
  });
}

export function useCardioTrackTemplates() {
  return useQuery({ queryKey: cardioTrackKeys.templates(), queryFn: () => cardioTracksApi.templates(), staleTime: Infinity });
}

export function useTodayCardio() {
  const sync = useOfflineSync();
  return useQuery({
    queryKey: cardioTrackKeys.today(),
    queryFn: async () => overlayCardioOps(await cardioTracksApi.today(), await sync.queue.all()),
    retry: false,
  });
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

/**
 * Logs a track session. Offline (or when the request dies on the wire) it is queued as a `cardio.log` op;
 * the request's `clientKey` (made when the sheet opened) keeps a replay to one session on the server.
 * A queued log shows straight away as "Waiting to sync" on the track and in the offline badge.
 */
export function useLogTrackSession() {
  const queryClient = useQueryClient();
  const sync = useOfflineSync();
  return useOfflineMutation({
    run: ({ id, request }: { id: string; request: LogTrackSessionRequest; label?: string }, key) => cardioTracksApi.logSession(id, request, key),
    toOp: ({ id, request, label }) => ({
      kind: 'cardio.log' as const,
      localDate: request.date ?? todayLocal(),
      label,
      target: { trackId: id },
      payload: request,
    }),
    onSuccess: async (outcome) => {
      if (outcome.queued) {
        const ops = await sync.queue.all();
        queryClient.setQueryData<TodayCardioDto>(cardioTrackKeys.today(), (d) => (d ? overlayCardioOps(d, ops) : d));
        queryClient.setQueryData<CardioTrackDto[]>(cardioTrackKeys.list(), (d) => (d ? overlayCardioOps({ tracks: d }, ops).tracks : d));
        return;
      }
      await queryClient.invalidateQueries({ queryKey: cardioTrackKeys.all });
    },
  });
}

export function useDecideSuggestion() {
  return useTrackMutation(({ id, accept }: { id: string; accept: boolean }) => cardioTracksApi.decide(id, accept));
}

/** Accepts the Start suggestion of a gated track. */
export function useStartGatedTrack() {
  return useTrackMutation((id: string) => cardioTracksApi.start(id));
}

/** Applies the template's start gate (Walk → run: the running gate) to a track. */
export function useApplyDefaultGate() {
  return useTrackMutation((id: string) => cardioTracksApi.setGate(id, null, true));
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
