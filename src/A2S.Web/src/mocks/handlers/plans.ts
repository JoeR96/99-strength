/**
 * Story/test handlers for `plansApi`. The repo stubs API modules directly
 * (see `DashboardPage.stories.tsx`) rather than intercepting HTTP, so each
 * handler set is a `Partial<typeof plansApi>` applied with `installHandlers`.
 */

import { plansApi } from '@/api/plans';
import { todayLocal, type LocalDate } from '@/lib/localDate';
import type { TodayDto, TrainingPlanDto } from '@/types/plan';
import {
  pausedPlanFixture,
  planFixture,
  todayCardio,
  todayDone,
  todayPaused,
  todayRest,
  todayUpperA,
} from '../fixtures/plan';
import { installHandlers, never, rejectWith } from './install';

type PlansHandlers = Partial<typeof plansApi>;

function withToday(build: (date: LocalDate) => TodayDto, plan: TrainingPlanDto | null = planFixture): PlansHandlers {
  return {
    getActive: async () => plan,
    getToday: async (date?: LocalDate) => build(date ?? todayLocal()),
    getWeeklyGoals: async (date?: LocalDate) => build(date ?? todayLocal()).week,
    completeSession: async (_planId, request) => ({
      id: 'comp-new',
      date: request.date,
      kind: request.kind,
      liftDay: request.liftDay ?? null,
      cardioProtocolId: request.cardioProtocolId ?? null,
      mobilityRoutineId: request.mobilityRoutineId ?? null,
      source: 0,
      hevyWorkoutId: null,
      recordedAtUtc: new Date().toISOString(),
      cardio: request.cardio ?? null,
      drillTicks: request.drillTicks ?? null,
    }),
    undoCompletion: async () => {},
    useAllowance: async (_planId, request) => request,
    removeAllowanceUse: async () => {},
  };
}

export const plansHandlers = {
  /** Monday, Upper A planned. */
  default: (): PlansHandlers => withToday(todayUpperA),
  done: (): PlansHandlers => withToday(todayDone),
  rest: (): PlansHandlers => withToday(todayRest),
  cardio: (): PlansHandlers => withToday(todayCardio),
  paused: (): PlansHandlers => withToday(todayPaused, pausedPlanFixture(todayLocal())),
  /** First run: no plan yet (`/plans/active` and `/plans/today` are 404 → null). */
  noPlan: (): PlansHandlers => ({
    getActive: async () => null,
    getToday: async () => null,
    getWeeklyGoals: rejectWith(404),
  }),
  loading: (): PlansHandlers => ({ getActive: never, getToday: never, getWeeklyGoals: never }),
  error: (): PlansHandlers => ({ getActive: rejectWith(500), getToday: rejectWith(500), getWeeklyGoals: rejectWith(500) }),
};

export function installPlansHandlers(handlers: PlansHandlers = plansHandlers.default()): void {
  installHandlers(plansApi, handlers);
}
