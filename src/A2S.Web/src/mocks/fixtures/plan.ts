/**
 * Plan fixtures matching the brief's seed ("Cut to 85 — Nov 2026"):
 * Mon Upper A · Tue Lower A · Wed 4×4 + Mobility B · Thu Upper B · Fri Lower B · Sat C25K/LISS · Sun Rest.
 */

import { addDays, isoWeekday, weekStartLocal, type LocalDate } from '@/lib/localDate';
import {
  PhaseType,
  SessionKind,
  SessionStatus,
  type TodayDto,
  type TodaySessionDto,
  type TrainingPlanDto,
  type WeeklyGoalsDto,
} from '@/types/plan';

export const PLAN_ID = '0f1a4c2e-7b3d-4e5f-9a1b-2c3d4e5f6a7b';
export const CARDIO_4X4_ID = 'c4c4c4c4-0000-4000-8000-000000000001';
export const CARDIO_C25K_ID = 'c25kc25k-0000-4000-8000-000000000002';
export const MOBILITY_A_ID = 'mob0000a-0000-4000-8000-000000000001';
export const MOBILITY_B_ID = 'mob0000b-0000-4000-8000-000000000002';

export const planFixture: TrainingPlanDto = {
  id: PLAN_ID,
  name: 'Cut to 85 — Nov 2026',
  startDate: '2026-11-09',
  isActive: true,
  linkedWorkoutId: 'w-1',
  startWeightKg: 95,
  allowances: { flexibleMeals: 1, alcoholOccasions: 1 },
  highIntensityTarget: { minMinutes: 15, maxMinutes: 30 },
  schedule: [
    { day: 1, kind: SessionKind.Lift, liftDay: 1, cardioProtocolId: null, mobilityRoutineId: null, label: 'Upper A' },
    { day: 2, kind: SessionKind.Lift, liftDay: 2, cardioProtocolId: null, mobilityRoutineId: null, label: 'Lower A' },
    { day: 3, kind: SessionKind.Cardio, liftDay: null, cardioProtocolId: CARDIO_4X4_ID, mobilityRoutineId: null, label: 'Norwegian 4×4' },
    { day: 3, kind: SessionKind.Mobility, liftDay: null, cardioProtocolId: null, mobilityRoutineId: MOBILITY_B_ID, label: 'Mobility B' },
    { day: 4, kind: SessionKind.Lift, liftDay: 3, cardioProtocolId: null, mobilityRoutineId: null, label: 'Upper B' },
    { day: 5, kind: SessionKind.Lift, liftDay: 4, cardioProtocolId: null, mobilityRoutineId: null, label: 'Lower B' },
    { day: 6, kind: SessionKind.Cardio, liftDay: null, cardioProtocolId: CARDIO_C25K_ID, mobilityRoutineId: null, label: 'C25K run' },
    { day: 0, kind: SessionKind.Rest, liftDay: null, cardioProtocolId: null, mobilityRoutineId: null, label: 'Rest — walk only' },
  ],
  phases: [
    { type: PhaseType.Cut, from: '2026-11-09', to: '2026-12-21', note: null },
    { type: PhaseType.Maintenance, from: '2026-12-22', to: '2027-01-03', note: 'Hold, don’t gain' },
    { type: PhaseType.Cut, from: '2027-01-04', to: '2027-03-08', note: null },
  ],
  checkpoints: [
    { date: '2026-12-07', targetKg: 92, note: '4 cutting weeks' },
    { date: '2026-12-21', targetKg: 91, note: 'Cut pauses after this' },
    { date: '2027-02-01', targetKg: 88, note: null },
    { date: '2027-03-08', targetKg: 85, note: 'Goal' },
  ],
  completions: [],
  allowanceUses: [],
};

export function weeklyGoalsFixture(date: LocalDate, overrides: Partial<WeeklyGoalsDto> = {}): WeeklyGoalsDto {
  return {
    weekStart: weekStartLocal(date),
    liftsPlanned: 4,
    liftsDone: 1,
    cardioPlanned: 2,
    cardioDone: 0,
    mobilityPlanned: 1,
    mobilityDone: 0,
    flexibleMealsUsed: 0,
    flexibleMealsAllowed: 1,
    alcoholOccasions: 0,
    alcoholAllowed: 1,
    highIntensityMinutesDone: 16,
    highIntensityMinutesMin: 15,
    highIntensityMinutesMax: 30,
    ...overrides,
  };
}

const upperA: TodaySessionDto = {
  kind: SessionKind.Lift,
  label: 'Upper A',
  status: SessionStatus.Planned,
  liftDay: 1,
  cardioProtocolId: null,
  mobilityRoutineId: null,
  completionId: null,
  hevyRoutineId: null,
};

/** Monday: Upper A planned. */
export function todayUpperA(date: LocalDate): TodayDto {
  return { date, phase: PhaseType.Cut, sessions: [upperA], week: weeklyGoalsFixture(date, { liftsDone: 0 }) };
}

export const HEVY_ROUTINE_ID = 'rt_upper_a_w1';

/** Monday: Upper A planned and already pushed to Hevy, so the card offers Open in Hevy. */
export function todayUpperAWithHevy(date: LocalDate): TodayDto {
  return {
    date,
    phase: PhaseType.Cut,
    sessions: [{ ...upperA, hevyRoutineId: HEVY_ROUTINE_ID }],
    week: weeklyGoalsFixture(date, { liftsDone: 0 }),
  };
}

/**
 * Any date, built from the plan's weekly schedule (Mon Upper A … Sun Rest), every session still
 * planned. For views that ask about a day other than today, such as the Hevy inbox's Assign sheet.
 */
export function dayFromSchedule(date: LocalDate): TodayDto {
  // `isoWeekday` counts from Monday = 0; the schedule uses .NET's DayOfWeek (Sunday = 0).
  const weekday = (isoWeekday(date) + 1) % 7;
  const sessions = planFixture.schedule
    .filter((s) => s.day === weekday)
    .map<TodaySessionDto>((s) => ({
      kind: s.kind,
      label: s.label,
      status: s.kind === SessionKind.Rest ? SessionStatus.Rest : SessionStatus.Planned,
      liftDay: s.liftDay,
      cardioProtocolId: s.cardioProtocolId,
      mobilityRoutineId: s.mobilityRoutineId,
      completionId: null,
      hevyRoutineId: null,
    }));
  return { date, phase: PhaseType.Cut, sessions, week: weeklyGoalsFixture(date) };
}

/** Monday after the lift has been logged. */
export function todayDone(date: LocalDate): TodayDto {
  return {
    date,
    phase: PhaseType.Cut,
    sessions: [{ ...upperA, status: SessionStatus.Done, completionId: 'comp-0001' }],
    week: weeklyGoalsFixture(date, { liftsDone: 1 }),
  };
}

/** Sunday: rest, walk only. */
export function todayRest(date: LocalDate): TodayDto {
  return {
    date,
    phase: PhaseType.Cut,
    sessions: [
      {
        kind: SessionKind.Rest,
        label: 'Rest — walk only',
        status: SessionStatus.Rest,
        liftDay: null,
        cardioProtocolId: null,
        mobilityRoutineId: null,
        completionId: null,
        hevyRoutineId: null,
      },
    ],
    week: weeklyGoalsFixture(date, { liftsDone: 4, cardioDone: 2, mobilityDone: 1 }),
  };
}

/** Wednesday: Norwegian 4×4 plus Mobility B. */
export function todayCardio(date: LocalDate): TodayDto {
  return {
    date,
    phase: PhaseType.Cut,
    sessions: [
      {
        kind: SessionKind.Cardio,
        label: 'Norwegian 4×4',
        status: SessionStatus.Planned,
        liftDay: null,
        cardioProtocolId: CARDIO_4X4_ID,
        mobilityRoutineId: null,
        completionId: null,
        hevyRoutineId: null,
      },
      {
        kind: SessionKind.Mobility,
        label: 'Mobility B',
        status: SessionStatus.Planned,
        liftDay: null,
        cardioProtocolId: null,
        mobilityRoutineId: MOBILITY_B_ID,
        completionId: null,
        hevyRoutineId: null,
      },
    ],
    week: weeklyGoalsFixture(date, { liftsDone: 2 }),
  };
}

/** A paused week (holiday): sessions are Paused and the week has nothing planned. */
export function todayPaused(date: LocalDate): TodayDto {
  return {
    date,
    phase: PhaseType.Pause,
    sessions: [{ ...upperA, status: SessionStatus.Paused }],
    week: weeklyGoalsFixture(date, {
      liftsPlanned: 0,
      liftsDone: 0,
      cardioPlanned: 0,
      cardioDone: 0,
      mobilityPlanned: 0,
      mobilityDone: 0,
      highIntensityMinutesDone: 0,
    }),
  };
}

/** The plan with a Pause phase covering `date` so the banner can show its end date. */
export function pausedPlanFixture(date: LocalDate): TrainingPlanDto {
  return {
    ...planFixture,
    phases: [...planFixture.phases, { type: PhaseType.Pause, from: addDays(date, -2), to: addDays(date, 5), note: 'Holiday' }],
  };
}
