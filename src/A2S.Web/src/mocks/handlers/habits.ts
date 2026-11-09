/**
 * Story/test handlers for `habitsApi`. The default set keeps an in-memory copy
 * of today's values so taps in Storybook behave like the real server
 * (increment returns the new stored value; a later refetch agrees with it).
 */

import { habitsApi } from '@/api/habits';
import { todayLocal, type LocalDate } from '@/lib/localDate';
import type { HabitDto, HabitLogDto, HabitsTodayDto } from '@/types/habits';
import { emptyHabitsToday, habitDefinitions, habitHistoryFixture, habitsTodayFixture } from '../fixtures/habits';
import { installHandlers, never, rejectWith } from './install';

type HabitsHandlers = Partial<typeof habitsApi>;

export interface StatefulHabitsOptions {
  /** Milliseconds each write waits before resolving (shows optimistic state in stories). */
  latencyMs?: number;
  /** Initial values per habit id. */
  values?: Partial<Record<string, number>>;
  /** Start with no habits at all. */
  empty?: boolean;
}

function delay(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}

export function statefulHabitsHandlers(options: StatefulHabitsOptions = {}): HabitsHandlers {
  const { latencyMs = 300, values = {}, empty = false } = options;
  let definitions: HabitDto[] = empty ? [] : habitDefinitions.map((h) => ({ ...h }));
  const logs = new Map<string, number>(); // `${habitId}|${date}` → value

  const seed = (date: LocalDate) => {
    if (empty) return;
    for (const h of habitsTodayFixture(date, values).habits) {
      const key = `${h.id}|${date}`;
      if (!logs.has(key)) logs.set(key, h.value);
    }
  };

  const today = (date: LocalDate): HabitsTodayDto => {
    seed(date);
    const base = empty ? emptyHabitsToday(date) : habitsTodayFixture(date, values);
    const habits = definitions
      .filter((d) => !d.archived)
      .map((d) => {
        const value = logs.get(`${d.id}|${date}`) ?? 0;
        return {
          id: d.id,
          name: d.name,
          type: d.type,
          target: d.target,
          minimum: d.minimum,
          unit: d.unit,
          value,
          metMinimum: value >= d.minimum,
          metTarget: value >= d.target,
          order: d.order,
          icon: d.icon,
        };
      });
    return { ...base, habits };
  };

  const log = (habitId: string, date: LocalDate, value: number): HabitLogDto => {
    logs.set(`${habitId}|${date}`, value);
    return { habitId, date, value, updatedAtUtc: new Date().toISOString() };
  };

  return {
    list: async (includeArchived = false) => definitions.filter((d) => includeArchived || !d.archived),
    getToday: async (date?: LocalDate) => today(date ?? todayLocal()),
    getHistory: async (from: LocalDate, to: LocalDate) => {
      const history = habitHistoryFixture(to, Math.max(1, Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000) + 1));
      return { ...history, habits: definitions };
    },
    increment: async (id, date, request) => {
      await delay(latencyMs);
      seed(date);
      const current = logs.get(`${id}|${date}`) ?? 0;
      return log(id, date, Math.max(0, current + request.delta));
    },
    setLog: async (id, date, request) => {
      await delay(latencyMs);
      return log(id, date, request.value);
    },
    create: async (request) => {
      const created: HabitDto = {
        id: `habit-new-${definitions.length + 1}`,
        name: request.name,
        type: request.type,
        target: request.target,
        minimum: request.minimum,
        unit: request.unit,
        order: request.order,
        archived: false,
        icon: request.icon,
        externalKey: null,
      };
      definitions = [...definitions, created];
      return created;
    },
    update: async (id, request) => {
      definitions = definitions.map((d) => (d.id === id ? { ...d, ...request } : d));
      return definitions.find((d) => d.id === id)!;
    },
    archive: async (id) => {
      definitions = definitions.map((d) => (d.id === id ? { ...d, archived: true } : d));
    },
    unarchive: async (id) => {
      definitions = definitions.map((d) => (d.id === id ? { ...d, archived: false } : d));
    },
  };
}

export const habitsHandlers = {
  default: (): HabitsHandlers => statefulHabitsHandlers(),
  empty: (): HabitsHandlers => statefulHabitsHandlers({ empty: true }),
  loading: (): HabitsHandlers => ({ getToday: never, getHistory: never, list: never }),
  error: (): HabitsHandlers => ({ getToday: rejectWith(500), getHistory: rejectWith(500), list: rejectWith(500) }),
  /** Reads work; every write fails, so optimistic updates roll back. */
  writesFail: (): HabitsHandlers => ({
    ...statefulHabitsHandlers(),
    increment: rejectWith(500),
    setLog: rejectWith(500),
  }),
};

export function installHabitsHandlers(handlers: HabitsHandlers = habitsHandlers.default()): void {
  installHandlers(habitsApi, handlers);
}
