/**
 * Habit queries and mutations.
 *
 * The `+` tap is optimistic: the cache changes before the request leaves, and a
 * failure rolls that one delta back with a toast. Several taps may be in flight
 * at once (double-tap), so the server's reply only replaces the cached value when
 * it is the last one outstanding for that habit; the cache is then invalidated so
 * the next read comes from the server. Date and `clientTimestamp` are fixed at
 * tap time so the offline queue can replay the same request later unchanged.
 *
 * Offline (or when the request dies on the wire) the tap is queued instead of
 * failing: the optimistic value stays, the pending badge counts it, and today's
 * query lays pending ops over whatever the server or the cache returns.
 */

import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useRef } from 'react';
import toast from 'react-hot-toast';
import { habitsApi } from '@/api/habits';
import { todayLocal, type LocalDate } from '@/lib/localDate';
import { useOfflineSync } from '@/offline/context';
import { overlayHabitOps } from '@/offline/overlay';
import { useOfflineMutation } from '@/offline/useOfflineMutation';
import type {
  CreateHabitRequest,
  HabitLogDto,
  HabitTodayDto,
  HabitsTodayDto,
  UpdateHabitRequest,
} from '@/types/habits';

export const habitKeys = {
  all: ['habits'] as const,
  list: (includeArchived: boolean) => [...habitKeys.all, 'list', includeArchived] as const,
  today: (date: LocalDate) => [...habitKeys.all, 'today', date] as const,
  history: (from: LocalDate, to: LocalDate) => [...habitKeys.all, 'history', from, to] as const,
  increment: () => [...habitKeys.all, 'increment'] as const,
};

export function useHabitsToday(date: LocalDate) {
  const sync = useOfflineSync();
  return useQuery({
    queryKey: habitKeys.today(date),
    queryFn: async () => overlayHabitOps(await habitsApi.getToday(date), await sync.queue.all()),
    staleTime: 60 * 1000,
  });
}

export function useHabits(includeArchived = false) {
  return useQuery({
    queryKey: habitKeys.list(includeArchived),
    queryFn: () => habitsApi.list(includeArchived),
  });
}

export function useHabitHistory(from: LocalDate, to: LocalDate, enabled = true) {
  return useQuery({
    queryKey: habitKeys.history(from, to),
    queryFn: () => habitsApi.getHistory(from, to),
    enabled,
  });
}

function applyValue(data: HabitsTodayDto | undefined, habitId: string, next: (habit: HabitTodayDto) => number) {
  if (!data) return data;
  return {
    ...data,
    habits: data.habits.map((h) => {
      if (h.id !== habitId) return h;
      const value = Math.max(0, next(h));
      return { ...h, value, metMinimum: value >= h.minimum, metTarget: value >= h.target };
    }),
  };
}

/** The habit's name from today's cache, for toasts about a dropped op. */
function habitName(queryClient: QueryClient, date: LocalDate, habitId: string): string | undefined {
  return queryClient.getQueryData<HabitsTodayDto>(habitKeys.today(date))?.habits.find((h) => h.id === habitId)?.name;
}

export interface IncrementVariables {
  habitId: string;
  delta: 1 | -1;
  /** Local date the tap belongs to; resolved at tap time. */
  date: LocalDate;
  /** ISO instant of the tap; the server keeps the newest. */
  clientTimestamp: string;
}

/**
 * The `+` / `−` tap. Call `tap(habitId, delta)`; the date and timestamp are
 * captured right then, so a tap at 23:59 and one at 00:01 land on different days.
 */
export function useIncrementHabit() {
  const queryClient = useQueryClient();
  const inFlight = useRef(new Map<string, number>());

  const mutation = useOfflineMutation<HabitLogDto, IncrementVariables, { key: string }>({
    mutationKey: habitKeys.increment(),
    run: ({ habitId, delta, date, clientTimestamp }) => habitsApi.increment(habitId, date, { delta, clientTimestamp }),
    toOp: ({ habitId, delta, date, clientTimestamp }) => ({
      kind: 'habit.increment',
      localDate: date,
      target: { habitId },
      payload: { delta, clientTimestamp },
      label: habitName(queryClient, date, habitId),
    }),
    onMutate: async ({ habitId, delta, date }) => {
      const key = `${habitId}|${date}`;
      inFlight.current.set(key, (inFlight.current.get(key) ?? 0) + 1);
      await queryClient.cancelQueries({ queryKey: habitKeys.today(date) });
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, (h) => h.value + delta)
      );
      return { key };
    },
    onSuccess: (outcome, { habitId, date }, context) => {
      if (outcome.queued) return;
      // Only the last outstanding tap for this habit may overwrite the cache with
      // the server's value; earlier replies are stale relative to later taps.
      if ((inFlight.current.get(context.key) ?? 0) <= 1) {
        queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
          applyValue(old, habitId, () => outcome.data.value)
        );
      }
    },
    onError: (_error, { habitId, delta, date }) => {
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, (h) => h.value - delta)
      );
      toast.error("Couldn't save that tap. Try again.");
    },
    onSettled: (outcome, _error, { date }, context) => {
      if (!context) return;
      const remaining = (inFlight.current.get(context.key) ?? 1) - 1;
      if (remaining <= 0) {
        inFlight.current.delete(context.key);
        // A queued tap is reconciled when it replays; refetching now would only find the cache.
        if (!outcome?.queued) void queryClient.invalidateQueries({ queryKey: habitKeys.today(date) });
      } else {
        inFlight.current.set(context.key, remaining);
      }
    },
  });

  const tap = (habitId: string, delta: 1 | -1) =>
    mutation.mutate({ habitId, delta, date: todayLocal(), clientTimestamp: new Date().toISOString() });

  return { ...mutation, tap };
}

export interface SetValueVariables {
  habitId: string;
  value: number;
  date: LocalDate;
  clientTimestamp: string;
}

/** Sets a day's value (numeric entry, checkbox toggle). Optimistic, with rollback; queued when offline. */
export function useSetHabitValue() {
  const queryClient = useQueryClient();

  const mutation = useOfflineMutation<HabitLogDto, SetValueVariables, { previous: HabitsTodayDto | undefined }>({
    run: ({ habitId, value, date, clientTimestamp }) => habitsApi.setLog(habitId, date, { value, clientTimestamp }),
    toOp: ({ habitId, value, date, clientTimestamp }) => ({
      kind: 'habit.set',
      localDate: date,
      target: { habitId },
      payload: { value, clientTimestamp },
      label: habitName(queryClient, date, habitId),
    }),
    onMutate: async ({ habitId, value, date }) => {
      await queryClient.cancelQueries({ queryKey: habitKeys.today(date) });
      const previous = queryClient.getQueryData<HabitsTodayDto>(habitKeys.today(date));
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) => applyValue(old, habitId, () => value));
      return { previous };
    },
    onSuccess: (outcome, { habitId, date }) => {
      if (outcome.queued) return;
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, () => outcome.data.value)
      );
    },
    onError: (_error, { date }, context) => {
      if (context?.previous) queryClient.setQueryData(habitKeys.today(date), context.previous);
      toast.error("Couldn't save that. Try again.");
    },
    onSettled: (outcome, _error, { date }) => {
      if (!outcome?.queued) void queryClient.invalidateQueries({ queryKey: habitKeys.today(date) });
    },
  });

  const set = (habitId: string, value: number, date: LocalDate = todayLocal()) =>
    mutation.mutate({ habitId, value, date, clientTimestamp: new Date().toISOString() });

  return { ...mutation, set };
}

export function useCreateHabit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateHabitRequest) => habitsApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: habitKeys.all }),
  });
}

export function useUpdateHabit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateHabitRequest }) => habitsApi.update(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: habitKeys.all }),
  });
}

export function useArchiveHabit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, archive }: { id: string; archive: boolean }) =>
      archive ? habitsApi.archive(id) : habitsApi.unarchive(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: habitKeys.all }),
  });
}
