/** Deterministic training-cycle data for the Current workout page (stories and tests). */

import type { CycleDto, CycleSessionDto, PlannedCardioDto, PlannedExerciseDto, WeekPlanDto } from '@/api/workouts';
import { MOBILITY_A_ID, MOBILITY_B_ID } from './plan';

const LABELS = ['Upper A', 'Lower A', 'Upper B', 'Lower B'];

export const stairCardioItem: PlannedCardioDto = {
  kind: 'Track',
  name: 'Stair finisher',
  prescription: '5 min continuous @ level 6',
  progressionType: 'Cardio track: double progression',
  orderInDay: 3,
  minutes: 5,
  intensityUnit: 'Level',
  intensity: 6,
  modality: 'StairClimber',
  stageName: 'Continuous',
  sessionsToStepUp: 2,
  stepUpNote: '2 more sessions to step up',
  cardioTrackId: 'track-stairs',
};

export const treadmillCardioItem: PlannedCardioDto = {
  kind: 'Fixed',
  name: 'Treadmill',
  prescription: '10 min · Easy incline',
  progressionType: 'Fixed',
  orderInDay: 3,
  minutes: 10,
  modality: 'Treadmill',
};

function linear(name: string, weightKg: number, reps: number, opts: { pending?: boolean; deload?: boolean } = {}): PlannedExerciseDto {
  const count = opts.deload ? 4 : 5;
  return {
    exerciseId: `ex-${name.toLowerCase().replace(/[^a-z]+/g, '-')}`,
    name,
    category: 'MainLift',
    equipment: 'Barbell',
    orderInDay: 1,
    hevyExerciseTemplateId: 'T',
    progressionType: 'Linear',
    restSeconds: 180,
    plannedSets: Array.from({ length: count }, (_, i) => ({
      setNumber: i + 1,
      weightKg: opts.pending ? 0 : weightKg,
      weightLbs: 0,
      originalUnit: 'Kilograms',
      targetReps: !opts.deload && i === count - 1 ? reps * 2 : reps,
      isAmrap: !opts.deload && i === count - 1,
    })),
    metadata: opts.pending ? { isTrainingMaxPending: true, intensityPercentage: 0.7 } : {},
  };
}

function rps(name: string, weightKg: number): PlannedExerciseDto {
  return {
    exerciseId: `ex-${name.toLowerCase().replace(/[^a-z]+/g, '-')}`,
    name,
    category: 'Accessory',
    equipment: 'Cable',
    orderInDay: 2,
    hevyExerciseTemplateId: 'T',
    progressionType: 'RepsPerSet',
    restSeconds: 90,
    plannedSets: [1, 2, 3].map((n) => ({ setNumber: n, weightKg, weightLbs: 0, originalUnit: 'Kilograms', targetReps: 12, isAmrap: false })),
    metadata: { repRangeMinimum: 8, repRangeMaximum: 12 },
  };
}

const MAIN = [
  ['Bench Press (Barbell)', 75],
  ['Squat (Barbell)', 100],
  ['Overhead Press (Barbell)', 45],
  ['Deadlift (Barbell)', 130],
] as const;
const ACCESSORY = [
  ['Seated Cable Row', 50],
  ['Lying Leg Curl (Machine)', 35],
  ['Lat Pulldown', 55],
  ['Hip Thrust (Machine)', 80],
] as const;

export interface CycleOptions {
  week?: number;
  /** The current day (1–4). Days before it in the week are done. */
  day?: number;
  deload?: boolean;
  pendingTm?: boolean;
}

export function sessionPlan(week: number, day: number, opts: { deload?: boolean; pendingTm?: boolean; current?: boolean } = {}): WeekPlanDto {
  const [main, mainKg] = MAIN[day - 1];
  const [acc, accKg] = ACCESSORY[day - 1];
  return {
    workoutId: 'w-1',
    workoutName: 'Cut to 85 — A2S2',
    weekNumber: week,
    dayNumber: day,
    blockNumber: 1,
    isDeloadWeek: opts.deload ?? false,
    intensityPercentage: opts.deload ? 58 : 75,
    exercises: [linear(main, mainKg, opts.deload ? 5 : 5, { pending: opts.pendingTm && day === 1, deload: opts.deload }), rps(acc, accKg)],
    cardio: day % 2 === 1 ? stairCardioItem : treadmillCardioItem,
    sessionLabel: LABELS[day - 1],
    warmUpRoutineId: MOBILITY_A_ID,
    pairedRoutineId: day % 2 === 1 ? MOBILITY_B_ID : null,
    isCurrent: opts.current ?? false,
  };
}

/** Week 1 Session 1 by default; `day` moves the current session; `deload` marks the current week as deload. */
export function cycleFixture({ week = 1, day = 1, deload = false, pendingTm = false }: CycleOptions = {}): CycleDto {
  const sessions: CycleSessionDto[] = [];
  const order = [day, ...[1, 2, 3, 4].filter((d) => d > day)];
  for (const d of order) {
    const current = d === day;
    sessions.push({ week, day: d, position: d, isCurrent: current, isDeloadWeek: deload, label: LABELS[d - 1], plan: sessionPlan(week, d, { deload, pendingTm, current }) });
  }
  for (const d of [1, 2, 3, 4]) {
    sessions.push({ week: week + 1, day: d, position: d, isCurrent: false, isDeloadWeek: false, label: LABELS[d - 1], plan: sessionPlan(week + 1, d) });
  }
  return { workoutId: 'w-1', workoutName: 'Cut to 85 — A2S2', currentWeek: week, currentDay: day, totalWeeks: 21, daysPerWeek: 4, isCompleted: false, sessions };
}
