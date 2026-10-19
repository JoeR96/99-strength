/**
 * Mock program fixture: one believable lifter in week 11 of a 21-week, 4-day A2S 2.0
 * hypertrophy program (block 2, just after the first deload).
 *
 * Everything is generated from a seeded PRNG and the app's own week table
 * (`getWeekParameters`), so working weights, AMRAP targets and training-max changes
 * line up with what the session page computes. Dates are relative to "now" so the
 * history calendar always ends at the current month.
 */
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import type {
  WorkoutDto,
  ExerciseDto,
  WorkoutSummaryDto,
  WorkoutHistoryResponse,
  WorkoutActivityHistoryDto,
  ExerciseHistoryDto,
  WeeklyPerformanceDto,
  CompletedSetHistoryDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from '@/types/workout';
import {
  EquipmentType,
  ExerciseCategory,
  ProgramVariant,
  WorkoutStatus,
  WeightUnit,
} from '@/types/workout';
import { createRandom } from './random';

export const PROGRAM_ID = 'wk-a2s-hypertrophy-2026';
export const PROGRAM_NAME = 'A2S Hypertrophy';
export const CURRENT_WEEK = 11;
export const CURRENT_DAY = 2;
export const DAYS_PER_WEEK = 4;
export const TOTAL_WEEKS = 21;

type LinearSpec = { type: 'Linear'; tm: number; tier: 'T1' | 'T2' };
type RpsSpec = {
  type: 'RepsPerSet';
  repMin: number;
  repMax: number;
  startSets: number;
  targetSets: number;
  weight: number;
  increment: number;
  unilateral?: boolean;
};
type MinimalSpec = {
  type: 'MinimalSets';
  target: number;
  startSets: number;
  minSets: number;
  maxSets: number;
  weight: number;
};

export interface ExerciseSpec {
  id: string;
  name: string;
  hevyId: string;
  day: 1 | 2 | 3 | 4;
  order: number;
  equipment: EquipmentType;
  spec: LinearSpec | RpsSpec | MinimalSpec;
}

/** The lifter's program. Training maxes are week-1 values; history moves them on. */
export const EXERCISE_SPECS: ExerciseSpec[] = [
  // Day 1 — squat
  {
    id: 'ex-squat',
    name: 'Squat (Barbell)',
    hevyId: 'D04AC939',
    day: 1,
    order: 1,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 130, tier: 'T1' },
  },
  {
    id: 'ex-rdl',
    name: 'Romanian Deadlift (Barbell)',
    hevyId: '2B4B7310',
    day: 1,
    order: 2,
    equipment: EquipmentType.Barbell,
    spec: {
      type: 'RepsPerSet',
      repMin: 8,
      repMax: 12,
      startSets: 3,
      targetSets: 5,
      weight: 80,
      increment: 5,
    },
  },
  {
    id: 'ex-leg-ext',
    name: 'Leg Extension (Machine)',
    hevyId: '75A4F6C4',
    day: 1,
    order: 3,
    equipment: EquipmentType.Machine,
    spec: {
      type: 'RepsPerSet',
      repMin: 10,
      repMax: 15,
      startSets: 3,
      targetSets: 5,
      weight: 55,
      increment: 5,
    },
  },
  {
    id: 'ex-leg-curl',
    name: 'Lying Leg Curl (Machine)',
    hevyId: 'B8127AD1',
    day: 1,
    order: 4,
    equipment: EquipmentType.Machine,
    spec: {
      type: 'RepsPerSet',
      repMin: 10,
      repMax: 15,
      startSets: 3,
      targetSets: 5,
      weight: 40,
      increment: 5,
    },
  },
  {
    id: 'ex-crunch',
    name: 'Cable Crunch',
    hevyId: '23A48484',
    day: 1,
    order: 5,
    equipment: EquipmentType.Cable,
    spec: {
      type: 'RepsPerSet',
      repMin: 12,
      repMax: 20,
      startSets: 3,
      targetSets: 4,
      weight: 35,
      increment: 5,
    },
  },
  // Day 2 — bench
  {
    id: 'ex-bench',
    name: 'Bench Press (Barbell)',
    hevyId: '79D0BB3A',
    day: 2,
    order: 1,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 92.5, tier: 'T1' },
  },
  {
    id: 'ex-row',
    name: 'Bent Over Row (Barbell)',
    hevyId: '55E6546F',
    day: 2,
    order: 2,
    equipment: EquipmentType.Barbell,
    spec: {
      type: 'RepsPerSet',
      repMin: 8,
      repMax: 12,
      startSets: 3,
      targetSets: 5,
      weight: 70,
      increment: 2.5,
    },
  },
  {
    id: 'ex-lateral',
    name: 'Lateral Raise (Dumbbell)',
    hevyId: '422B08F1',
    day: 2,
    order: 3,
    equipment: EquipmentType.Dumbbell,
    spec: {
      type: 'RepsPerSet',
      repMin: 12,
      repMax: 20,
      startSets: 3,
      targetSets: 5,
      weight: 10,
      increment: 2,
    },
  },
  {
    id: 'ex-pushdown',
    name: 'Triceps Rope Pushdown',
    hevyId: '94B7239B',
    day: 2,
    order: 4,
    equipment: EquipmentType.Cable,
    spec: {
      type: 'RepsPerSet',
      repMin: 12,
      repMax: 15,
      startSets: 3,
      targetSets: 5,
      weight: 25,
      increment: 2.5,
    },
  },
  {
    id: 'ex-pullup',
    name: 'Pull Up (Weighted)',
    hevyId: '729237D1',
    day: 2,
    order: 5,
    equipment: EquipmentType.Bodyweight,
    spec: { type: 'MinimalSets', target: 30, startSets: 5, minSets: 3, maxSets: 6, weight: 10 },
  },
  // Day 3 — deadlift
  {
    id: 'ex-deadlift',
    name: 'Deadlift (Barbell)',
    hevyId: 'C6272009',
    day: 3,
    order: 1,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 170, tier: 'T1' },
  },
  {
    id: 'ex-front-squat',
    name: 'Front Squat',
    hevyId: '5046D0A9',
    day: 3,
    order: 2,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 95, tier: 'T2' },
  },
  {
    id: 'ex-seated-curl',
    name: 'Seated Leg Curl (Machine)',
    hevyId: '11A123F3',
    day: 3,
    order: 3,
    equipment: EquipmentType.Machine,
    spec: {
      type: 'RepsPerSet',
      repMin: 10,
      repMax: 15,
      startSets: 3,
      targetSets: 5,
      weight: 45,
      increment: 5,
    },
  },
  {
    id: 'ex-face-pull',
    name: 'Face Pull',
    hevyId: 'BE640BA0',
    day: 3,
    order: 4,
    equipment: EquipmentType.Cable,
    spec: {
      type: 'RepsPerSet',
      repMin: 15,
      repMax: 20,
      startSets: 3,
      targetSets: 4,
      weight: 20,
      increment: 2.5,
    },
  },
  // Day 4 — press
  {
    id: 'ex-ohp',
    name: 'Overhead Press (Barbell)',
    hevyId: '7B8D84E8',
    day: 4,
    order: 1,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 57.5, tier: 'T1' },
  },
  {
    id: 'ex-incline',
    name: 'Incline Bench Press (Barbell)',
    hevyId: '50DFDFAB',
    day: 4,
    order: 2,
    equipment: EquipmentType.Barbell,
    spec: { type: 'Linear', tm: 75, tier: 'T2' },
  },
  {
    id: 'ex-pulldown',
    name: 'Lat Pulldown (Cable)',
    hevyId: '6A6C31A5',
    day: 4,
    order: 3,
    equipment: EquipmentType.Cable,
    spec: {
      type: 'RepsPerSet',
      repMin: 8,
      repMax: 12,
      startSets: 3,
      targetSets: 5,
      weight: 60,
      increment: 5,
    },
  },
  {
    id: 'ex-curl',
    name: 'Bicep Curl (Dumbbell)',
    hevyId: '37FCC2BB',
    day: 4,
    order: 4,
    equipment: EquipmentType.Dumbbell,
    spec: {
      type: 'RepsPerSet',
      repMin: 10,
      repMax: 15,
      startSets: 2,
      targetSets: 3,
      weight: 14,
      increment: 2,
      unilateral: true,
    },
  },
];

const DAY_NAMES = ['Monday', 'Tuesday', 'Thursday', 'Friday'];
/** Training days: Mon, Tue, Thu, Fri. */
const DAY_OFFSETS = [0, 1, 3, 4];

const round1 = (n: number) => Math.round(n * 10) / 10;

/** Monday of the current week, 00:00 local. */
function currentMonday(now = new Date()): Date {
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const dow = (d.getDay() + 6) % 7; // Mon = 0
  d.setDate(d.getDate() - dow);
  return d;
}

/** Program start: Monday of week 1. */
export const PROGRAM_START = (() => {
  const d = currentMonday();
  d.setDate(d.getDate() - (CURRENT_WEEK - 1) * 7);
  return d;
})();

export function sessionDate(week: number, day: number, hour = 7, minute = 30): Date {
  const d = new Date(PROGRAM_START);
  d.setDate(d.getDate() + (week - 1) * 7 + DAY_OFFSETS[day - 1]);
  d.setHours(hour, minute, 0, 0);
  return d;
}

const blockOf = (week: number) => Math.ceil(week / 7);

/** AMRAP delta → TM change (AmrapDeltaTable, see AGENTS.md). */
function tmAdjustment(delta: number): number {
  if (delta >= 5) return 0.03;
  if (delta >= 3) return 0.02;
  if (delta >= 1) return 0.01;
  if (delta === 0) return 0;
  if (delta === -1) return -0.02;
  return -0.05;
}

interface SessionRecord {
  week: number;
  day: number;
  completedAt: string;
  sets: CompletedSetHistoryDto[];
  tmAtWeek?: number;
  weightAtWeek?: number;
  setCountAtWeek?: number;
  amrapReps?: number;
}

interface ExerciseState {
  spec: ExerciseSpec;
  tm: number;
  weight: number;
  sets: number;
  sessions: SessionRecord[];
}

function kgSet(
  setNumber: number,
  weight: number,
  reps: number,
  wasAmrap = false
): CompletedSetHistoryDto {
  return { setNumber, weight, weightUnit: 'Kilograms', actualReps: reps, wasAmrap };
}

/** Run the program forward from week 1 to the current session. */
function simulate(): ExerciseState[] {
  const rand = createRandom(99);
  const states: ExerciseState[] = EXERCISE_SPECS.map((spec) => ({
    spec,
    tm: spec.spec.type === 'Linear' ? spec.spec.tm : 0,
    weight: spec.spec.type === 'Linear' ? 0 : spec.spec.weight,
    sets:
      spec.spec.type === 'RepsPerSet' || spec.spec.type === 'MinimalSets' ? spec.spec.startSets : 4,
    sessions: [],
  }));

  for (let week = 1; week <= CURRENT_WEEK; week++) {
    const params = getWeekParameters(week);
    for (let day = 1; day <= DAYS_PER_WEEK; day++) {
      if (week === CURRENT_WEEK && day >= CURRENT_DAY) break;
      const completedAt = sessionDate(week, day).toISOString();
      for (const state of states.filter((s) => s.spec.day === day)) {
        const { spec } = state;
        if (spec.spec.type === 'Linear') {
          const weight = roundToGymIncrement(state.tm * params.intensity);
          const sets: CompletedSetHistoryDto[] = [];
          let amrapReps: number | undefined;
          for (let n = 1; n <= params.sets; n++) {
            const isAmrap = !params.isDeload && n === params.sets;
            if (isAmrap) {
              // Mostly beats the rep-out target; the odd bad day.
              const delta = rand.pick([-1, 0, 1, 1, 2, 2, 2, 3, 3, 4, 5]);
              amrapReps = (params.repOutTarget ?? params.targetReps) + delta;
              sets.push(kgSet(n, weight, amrapReps, true));
            } else {
              sets.push(kgSet(n, weight, params.targetReps));
            }
          }
          state.sessions.push({
            week,
            day,
            completedAt,
            sets,
            tmAtWeek: round1(state.tm),
            weightAtWeek: weight,
            setCountAtWeek: params.sets,
            amrapReps,
          });
          if (amrapReps !== undefined && params.repOutTarget !== null) {
            state.tm = round1(state.tm * (1 + tmAdjustment(amrapReps - params.repOutTarget)));
          }
        } else if (spec.spec.type === 'RepsPerSet') {
          const s = spec.spec;
          const outcome = params.isDeload
            ? 'maintained'
            : rand.pick(['success', 'success', 'maintained', 'maintained', 'maintained', 'failed']);
          const sets: CompletedSetHistoryDto[] = [];
          for (let n = 1; n <= state.sets; n++) {
            let reps = s.repMax;
            if (outcome === 'maintained')
              reps = n === 1 ? s.repMax : rand.int(s.repMin + 1, s.repMax);
            if (outcome === 'maintained' && n === state.sets) reps = Math.min(reps, s.repMax - 1);
            if (outcome === 'failed')
              reps = n === state.sets ? s.repMin - 1 : rand.int(s.repMin, s.repMax - 1);
            sets.push(kgSet(n, state.weight, reps));
          }
          state.sessions.push({
            week,
            day,
            completedAt,
            sets,
            weightAtWeek: state.weight,
            setCountAtWeek: state.sets,
          });
          if (outcome === 'success') {
            if (state.sets < s.targetSets) state.sets += 1;
            else {
              state.weight = round1(state.weight + s.increment);
              state.sets = s.startSets;
            }
          } else if (outcome === 'failed') {
            if (state.sets > s.startSets) state.sets -= 1;
          }
        } else {
          const s = spec.spec;
          const perSet = Math.ceil(s.target / state.sets);
          const sets: CompletedSetHistoryDto[] = [];
          let left = s.target;
          for (let n = 1; n <= state.sets; n++) {
            const reps = Math.min(left, perSet + (n === 1 ? 1 : 0));
            left -= reps;
            sets.push(kgSet(n, s.weight, Math.max(reps, 1)));
          }
          state.sessions.push({
            week,
            day,
            completedAt,
            sets,
            weightAtWeek: s.weight,
            setCountAtWeek: state.sets,
          });
          if (!params.isDeload && week % 3 === 0 && state.sets > s.minSets) state.sets -= 1;
        }
      }
    }
  }
  return states;
}

const STATES = simulate();

function unitLabel(unit: WeightUnit) {
  return unit === WeightUnit.Kilograms ? 'Kilograms' : 'Pounds';
}

function categoryOf(spec: ExerciseSpec): ExerciseCategory {
  if (spec.spec.type !== 'Linear') return ExerciseCategory.Accessory;
  return spec.spec.tier === 'T1' ? ExerciseCategory.MainLift : ExerciseCategory.Auxiliary;
}

function progressionOf(state: ExerciseState): ExerciseDto['progression'] {
  const { spec } = state.spec;
  if (spec.type === 'Linear') {
    const p: LinearProgressionDto = {
      type: 'Linear',
      trainingMax: { value: state.tm, unit: WeightUnit.Kilograms },
      useAmrap: true,
      baseSetsPerExercise: 4,
    };
    return p;
  }
  if (spec.type === 'RepsPerSet') {
    const p: RepsPerSetProgressionDto = {
      type: 'RepsPerSet',
      repRange: { minimum: spec.repMin, maximum: spec.repMax },
      startingSets: spec.startSets,
      currentSetCount: state.sets,
      targetSets: spec.targetSets,
      currentWeight: state.weight,
      weightUnit: unitLabel(WeightUnit.Kilograms),
      isUnilateral: !!spec.unilateral,
      isWeightPending: false,
      pendingWeightConfirmation: false,
      suggestedWeight: null,
    };
    return p;
  }
  const p: MinimalSetsProgressionDto = {
    type: 'MinimalSets',
    currentWeight: spec.weight,
    weightUnit: 'Kilograms',
    targetTotalReps: spec.target,
    currentSetCount: state.sets,
    minimumSets: spec.minSets,
    maximumSets: spec.maxSets,
  };
  return p;
}

function exerciseDto(state: ExerciseState): ExerciseDto {
  const last = state.sessions[state.sessions.length - 1];
  return {
    id: state.spec.id,
    name: state.spec.name,
    category: categoryOf(state.spec),
    equipment: state.spec.equipment,
    assignedDay: state.spec.day,
    orderInDay: state.spec.order,
    hevyExerciseTemplateId: state.spec.hevyId,
    progression: progressionOf(state),
    lastPerformance: last
      ? {
          weekNumber: last.week,
          completedAt: last.completedAt,
          sets: last.sets.map((s) => ({
            setNumber: s.setNumber,
            weight: s.weight,
            weightUnit: s.weightUnit,
            reps: s.actualReps,
            wasAmrap: s.wasAmrap,
          })),
        }
      : null,
  };
}

function syncedRoutines(): Record<string, string> {
  const map: Record<string, string> = {};
  for (let day = 1; day <= DAYS_PER_WEEK; day++)
    map[`week${CURRENT_WEEK}-day${day}`] = `rt-w${CURRENT_WEEK}-d${day}`;
  for (let day = 1; day <= DAYS_PER_WEEK; day++)
    map[`week${CURRENT_WEEK - 1}-day${day}`] = `rt-w${CURRENT_WEEK - 1}-d${day}`;
  return map;
}

/** GET /workouts/current */
export const currentWorkout: WorkoutDto = {
  id: PROGRAM_ID,
  name: PROGRAM_NAME,
  variant: ProgramVariant.FourDay,
  status: WorkoutStatus.Active,
  currentWeek: CURRENT_WEEK,
  currentBlock: blockOf(CURRENT_WEEK),
  currentDay: CURRENT_DAY,
  daysPerWeek: DAYS_PER_WEEK,
  completedDaysInCurrentWeek: Array.from({ length: CURRENT_DAY - 1 }, (_, i) => i + 1),
  isWeekComplete: false,
  totalWeeks: TOTAL_WEEKS,
  blockSequence: [1, 2, 3],
  startDate: PROGRAM_START.toISOString(),
  createdAt: new Date(PROGRAM_START.getTime() - 2 * 86400000).toISOString(),
  startedAt: PROGRAM_START.toISOString(),
  exerciseCount: EXERCISE_SPECS.length,
  exercises: STATES.map(exerciseDto),
  hevyRoutineFolderId: '1184223',
  hevySyncedRoutines: syncedRoutines(),
};

function weeklyPerformance(session: SessionRecord): WeeklyPerformanceDto {
  const totalReps = session.sets.reduce((a, s) => a + s.actualReps, 0);
  const totalVolume = session.sets.reduce((a, s) => a + s.actualReps * s.weight, 0);
  const averageWeight = session.sets.reduce((a, s) => a + s.weight, 0) / session.sets.length;
  return {
    weekNumber: session.week,
    blockNumber: blockOf(session.week),
    completedAt: session.completedAt,
    isDeloadWeek: getWeekParameters(session.week).isDeload,
    totalVolume,
    averageWeight,
    totalReps,
    setsCompleted: session.sets.length,
    amrapReps: session.amrapReps,
    sets: session.sets,
    trainingMaxAtWeek: session.tmAtWeek,
    trainingMaxUnitAtWeek: session.tmAtWeek !== undefined ? 'Kilograms' : undefined,
    weightAtWeek: session.weightAtWeek,
    setCountAtWeek: session.setCountAtWeek,
    progressionTypeAtWeek: undefined,
  };
}

function exerciseHistory(state: ExerciseState): ExerciseHistoryDto {
  const { spec } = state;
  return {
    exerciseId: spec.id,
    name: spec.name,
    progressionType: spec.spec.type,
    assignedDay: spec.day,
    category:
      spec.spec.type === 'Linear'
        ? spec.spec.tier === 'T1'
          ? 'MainLift'
          : 'Auxiliary'
        : 'Accessory',
    equipment:
      Object.keys(EquipmentType).find(
        (k) => EquipmentType[k as keyof typeof EquipmentType] === spec.equipment
      ) ?? 'Barbell',
    currentWeight: spec.spec.type === 'Linear' ? 0 : state.weight,
    weightUnit: 'Kilograms',
    currentSets: state.sets,
    targetSets: spec.spec.type === 'RepsPerSet' ? spec.spec.targetSets : state.sets,
    trainingMax: spec.spec.type === 'Linear' ? state.tm : undefined,
    weeklyHistory: state.sessions.map(weeklyPerformance),
    progressionChanges: [],
  };
}

function activities(): WorkoutActivityHistoryDto[] {
  const out: WorkoutActivityHistoryDto[] = [];
  for (let week = 1; week <= CURRENT_WEEK; week++) {
    for (let day = 1; day <= DAYS_PER_WEEK; day++) {
      if (week === CURRENT_WEEK && day >= CURRENT_DAY) break;
      const completedAt = sessionDate(week, day).toISOString();
      out.push({
        day: `Day${day}`,
        dayNumber: day,
        weekNumber: week,
        blockNumber: blockOf(week),
        completedAt,
        isDeloadWeek: getWeekParameters(week).isDeload,
        performances: STATES.filter((s) => s.spec.day === day).map((s) => {
          const session = s.sessions.find((x) => x.week === week && x.day === day)!;
          return { exerciseId: s.spec.id, completedAt, completedSets: session.sets };
        }),
      });
    }
  }
  return out;
}

/** GET /workouts/history */
export const workoutHistory: WorkoutHistoryResponse = {
  workoutId: PROGRAM_ID,
  workoutName: PROGRAM_NAME,
  variant: 'FourDay',
  totalWeeks: TOTAL_WEEKS,
  currentWeek: CURRENT_WEEK,
  currentBlock: blockOf(CURRENT_WEEK),
  daysPerWeek: DAYS_PER_WEEK,
  startedAt: PROGRAM_START.toISOString(),
  totalWorkoutsCompleted: (CURRENT_WEEK - 1) * DAYS_PER_WEEK + (CURRENT_DAY - 1),
  completedActivities: activities(),
  exerciseHistories: STATES.map(exerciseHistory),
};

/** The previous, finished program — gives the Programs page and the long-range charts depth. */
const PREVIOUS_START = new Date(PROGRAM_START.getTime() - 24 * 7 * 86400000);

/** GET /workouts */
export const workoutSummaries: WorkoutSummaryDto[] = [
  {
    id: PROGRAM_ID,
    name: PROGRAM_NAME,
    variant: 'FourDay',
    totalWeeks: TOTAL_WEEKS,
    currentWeek: CURRENT_WEEK,
    currentBlock: blockOf(CURRENT_WEEK),
    currentDay: CURRENT_DAY,
    daysPerWeek: DAYS_PER_WEEK,
    completedDaysInCurrentWeek: currentWorkout.completedDaysInCurrentWeek,
    isWeekComplete: false,
    blockSequence: [1, 2, 3],
    status: 'Active',
    createdAt: currentWorkout.createdAt,
    startedAt: currentWorkout.startedAt,
    exerciseCount: EXERCISE_SPECS.length,
    isActive: true,
  },
  {
    id: 'wk-a2s-strength-spring',
    name: 'A2S Strength — Spring Block',
    variant: 'FourDay',
    totalWeeks: 21,
    currentWeek: 21,
    currentBlock: 3,
    currentDay: 4,
    daysPerWeek: 4,
    completedDaysInCurrentWeek: [1, 2, 3, 4],
    isWeekComplete: true,
    blockSequence: [1, 2, 3],
    status: 'Completed',
    createdAt: new Date(PREVIOUS_START.getTime() - 86400000).toISOString(),
    startedAt: PREVIOUS_START.toISOString(),
    completedAt: new Date(PREVIOUS_START.getTime() + 21 * 7 * 86400000).toISOString(),
    exerciseCount: 16,
    isActive: false,
  },
  {
    id: 'wk-upper-lower-5',
    name: 'Upper/Lower 5-Day (draft)',
    variant: 'FiveDay',
    totalWeeks: 28,
    currentWeek: 1,
    currentBlock: 1,
    currentDay: 1,
    daysPerWeek: 5,
    completedDaysInCurrentWeek: [],
    isWeekComplete: false,
    blockSequence: [1, 1, 2, 3],
    status: 'NotStarted',
    createdAt: new Date(PROGRAM_START.getTime() - 9 * 86400000).toISOString(),
    exerciseCount: 22,
    isActive: false,
  },
];

export const exerciseStates = STATES;
export { DAY_NAMES };
