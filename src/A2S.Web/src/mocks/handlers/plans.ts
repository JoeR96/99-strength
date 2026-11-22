/**
 * Story/test handlers for `plansApi`. The repo stubs API modules directly
 * (see `DashboardPage.stories.tsx`) rather than intercepting HTTP, so each
 * handler set is a `Partial<typeof plansApi>` applied with `installHandlers`.
 */

import { plansApi } from '@/api/plans';
import { addDays, todayLocal, type LocalDate } from '@/lib/localDate';
import { SessionKind, type TodayDto, type TrainingPlanDto, type UpcomingDayDto, type UpcomingLiftDto } from '@/types/plan';
import {
  dayFromSchedule,
  pausedPlanFixture,
  planFixture,
  todayCardio,
  todayDone,
  todayDoneNotInHevy,
  todayPaused,
  todayRest,
  todayUpperA,
  todayUpperAWithHevy,
} from '../fixtures/plan';
import { installHandlers, never, rejectWith } from './install';

type PlansHandlers = Partial<typeof plansApi>;

/** A lift's planned sets on `/plans/upcoming`: bench 5×5 @ 75 kg with the AMRAP last, then a seal row. */
export function upcomingLiftFixture(liftDay: number, label: string): UpcomingLiftDto {
  return {
    liftDay: liftDay as UpcomingLiftDto['liftDay'],
    label,
    weekNumber: 2,
    isDeloadWeek: false,
    exercises: [
      { name: 'Bench Press (Barbell)', category: 'MainLift', sets: [1, 2, 3, 4, 5].map((n) => ({ weightKg: 75, reps: n === 5 ? 10 : 5, isAmrap: n === 5 })) },
      { name: 'Seal Row (Barbell)', category: 'Accessory', sets: [1, 2, 3].map(() => ({ weightKg: 40, reps: 10, isAmrap: false })) },
    ],
    finisher: null,
  };
}

/** `days` days from `from`, each built from the weekly schedule, with lift blocks for lift days. */
export function upcomingFromSchedule(from: LocalDate, days: number): UpcomingDayDto[] {
  return Array.from({ length: days }, (_, i) => {
    const day = dayFromSchedule(addDays(from, i));
    const lifts = day.sessions.filter((s) => s.kind === SessionKind.Lift && s.liftDay).map((s) => upcomingLiftFixture(s.liftDay!, s.label));
    return { date: day.date, phase: day.phase, sessions: day.sessions, lifts };
  });
}

function withToday(build: (date: LocalDate) => TodayDto, plan: TrainingPlanDto | null = planFixture): PlansHandlers {
  return {
    getActive: async () => plan,
    getToday: async (date?: LocalDate) => build(date ?? todayLocal()),
    getWeeklyGoals: async (date?: LocalDate) => build(date ?? todayLocal()).week,
    getUpcoming: async (from?: LocalDate, days = 14) => upcomingFromSchedule(from ?? todayLocal(), days),
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
  /** Monday, Upper A planned and already pushed to Hevy. */
  withHevyRoutine: (): PlansHandlers => withToday(todayUpperAWithHevy),
  /** Each date answers from the weekly schedule (Mon Upper A … Sun Rest). */
  week: (): PlansHandlers => withToday(dayFromSchedule),
  done: (): PlansHandlers => withToday(todayDone),
  /** Upper A finished in gym mode; its Hevy post is still pending. */
  doneNotInHevy: (): PlansHandlers => withToday(todayDoneNotInHevy),
  rest: (): PlansHandlers => withToday(todayRest),
  cardio: (): PlansHandlers => withToday(todayCardio),
  paused: (): PlansHandlers => withToday(todayPaused, pausedPlanFixture(todayLocal())),
  /** First run: no plan yet (`/plans/active` and `/plans/today` are 404 → null). */
  noPlan: (): PlansHandlers => ({
    getActive: async () => null,
    getToday: async () => null,
    getUpcoming: async () => null,
    getWeeklyGoals: rejectWith(404),
  }),
  loading: (): PlansHandlers => ({ getActive: never, getToday: never, getWeeklyGoals: never, getUpcoming: never }),
  error: (): PlansHandlers => ({ getActive: rejectWith(500), getToday: rejectWith(500), getWeeklyGoals: rejectWith(500), getUpcoming: rejectWith(500) }),
};

export function installPlansHandlers(handlers: PlansHandlers = plansHandlers.default()): void {
  installHandlers(plansApi, handlers);
}
