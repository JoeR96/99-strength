/**
 * Habit queries and mutations.
 *
 * The `+` tap is optimistic: the cache changes before the request leaves, and a
 * failure rolls that one delta back with a toast. Several taps may be in flight
 * at once (double-tap), so the server's reply only replaces the cached value when
 * it is the last one outstanding for that habit; the cache is then invalidated so
 * the next read comes from the server. Date and `clientTimestamp` are fixed at
 * tap time so PR 7 can replay the same request later unchanged.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef } from 'react';
import toast from 'react-hot-toast';
import { habitsApi } from '@/api/habits';
import { todayLocal, type LocalDate } from '@/lib/localDate';
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
  return useQuery({
    queryKey: habitKeys.today(date),
    queryFn: () => habitsApi.getToday(date),
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

  const mutation = useMutation<HabitLogDto, Error, IncrementVariables, { key: string }>({
    mutationKey: habitKeys.increment(),
    mutationFn: ({ habitId, delta, date, clientTimestamp }) =>
      habitsApi.increment(habitId, date, { delta, clientTimestamp }),
    onMutate: async ({ habitId, delta, date }) => {
      const key = `${habitId}|${date}`;
      inFlight.current.set(key, (inFlight.current.get(key) ?? 0) + 1);
      await queryClient.cancelQueries({ queryKey: habitKeys.today(date) });
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, (h) => h.value + delta)
      );
      return { key };
    },
    onSuccess: (serverLog, { habitId, date }, context) => {
      // Only the last outstanding tap for this habit may overwrite the cache with
      // the server's value; earlier replies are stale relative to later taps.
      if ((inFlight.current.get(context.key) ?? 0) <= 1) {
        queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
          applyValue(old, habitId, () => serverLog.value)
        );
      }
    },
    onError: (_error, { habitId, delta, date }) => {
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, (h) => h.value - delta)
      );
      toast.error("Couldn't save that tap. Try again.");
    },
    onSettled: (_data, _error, { date }, context) => {
      if (!context) return;
      const remaining = (inFlight.current.get(context.key) ?? 1) - 1;
      if (remaining <= 0) {
        inFlight.current.delete(context.key);
        void queryClient.invalidateQueries({ queryKey: habitKeys.today(date) });
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

/** Sets a day's value (numeric entry, checkbox toggle). Optimistic, with rollback. */
export function useSetHabitValue() {
  const queryClient = useQueryClient();

  const mutation = useMutation<HabitLogDto, Error, SetValueVariables, { previous: HabitsTodayDto | undefined }>({
    mutationFn: ({ habitId, value, date, clientTimestamp }) =>
      habitsApi.setLog(habitId, date, { value, clientTimestamp }),
    onMutate: async ({ habitId, value, date }) => {
      await queryClient.cancelQueries({ queryKey: habitKeys.today(date) });
      const previous = queryClient.getQueryData<HabitsTodayDto>(habitKeys.today(date));
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) => applyValue(old, habitId, () => value));
      return { previous };
    },
    onSuccess: (serverLog, { habitId, date }) => {
      queryClient.setQueryData<HabitsTodayDto>(habitKeys.today(date), (old) =>
        applyValue(old, habitId, () => serverLog.value)
      );
    },
    onError: (_error, { date }, context) => {
      if (context?.previous) queryClient.setQueryData(habitKeys.today(date), context.previous);
      toast.error("Couldn't save that. Try again.");
    },
    onSettled: (_data, _error, { date }) => {
      void queryClient.invalidateQueries({ queryKey: habitKeys.today(date) });
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
