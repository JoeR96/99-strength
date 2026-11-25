import { stairTrack } from '@/mocks/fixtures/cardioTracks';
/** Deterministic gym-mode data for tests and stories: Upper A with a bench AMRAP, a seal row, Mobility A and a stair finisher. */

import type { PlannedCardioBlockDto, WeekPlanDto } from '@/api/workouts';
import type { MobilityRoutineDto } from '@/types/mobility';
import { DoseUnit, SideBias } from '@/types/mobility';
import { CardioProtocolKind, type CardioProtocolDto } from '@/types/gym';
import { createGymSession, toggleSet, tickDrill, type GymSessionState } from './gymSession';

export const GYM_CLIENT_SESSION = '9a9a9a9a-0000-4000-8000-000000000001';
export const GYM_STARTED_AT = '2026-11-16T17:55:00.000Z';

export const upperAPlan: WeekPlanDto = {
  workoutId: 'w-1',
  workoutName: 'Cut to 85',
  weekNumber: 3,
  dayNumber: 1,
  blockNumber: 1,
  isDeloadWeek: false,
  intensityPercentage: 75,
  exercises: [
    {
      exerciseId: 'ex-bench',
      name: 'Bench Press (Barbell)',
      category: 'MainLift',
      equipment: 'Barbell',
      orderInDay: 1,
      hevyExerciseTemplateId: '79D0BB3A',
      progressionType: 'Linear',
      restSeconds: 180,
      plannedSets: [1, 2, 3, 4, 5].map((n) => ({ setNumber: n, weightKg: 75, weightLbs: 165, originalUnit: 'Kilograms', targetReps: 5, isAmrap: n === 5 })),
      metadata: {},
    },
    {
      exerciseId: 'ex-row',
      name: 'Seal Row (Barbell)',
      category: 'Accessory',
      equipment: 'Barbell',
      orderInDay: 2,
      hevyExerciseTemplateId: 'custom:seal-row',
      progressionType: 'RepsPerSet',
      restSeconds: 90,
      plannedSets: [1, 2, 3].map((n) => ({ setNumber: n, weightKg: 40, weightLbs: 90, originalUnit: 'Kilograms', targetReps: 10, isAmrap: false })),
      metadata: {},
    },
  ],
};

export const mobilityA: MobilityRoutineDto = {
  id: 'mob-a',
  name: 'Mobility A',
  targetMinutes: 10,
  drills: [
    { name: 'Open Books', sets: 1, dose: { unit: DoseUnit.Reps, amount: 8 }, extraSetSide: SideBias.None, cues: null, standingAlternative: null, perSide: true },
    { name: 'Dead Hang', sets: 2, dose: { unit: DoseUnit.Seconds, amount: 30 }, extraSetSide: SideBias.None, cues: null, standingAlternative: null, perSide: false },
  ],
};

export const stairFinisher: CardioProtocolDto = {
  id: 'cp-stairs',
  name: 'Stair finisher',
  kind: CardioProtocolKind.Liss,
  intervals: [],
  totalMinutes: 10,
  notes: 'steady, level 8',
  weekIndex: null,
  repeatWeek: false,
};

export function freshGymSession(withFinisher = true): GymSessionState {
  return createGymSession({
    clientSessionId: GYM_CLIENT_SESSION,
    startedAt: GYM_STARTED_AT,
    title: 'Cut to 85 · W3 Upper A',
    plan: upperAPlan,
    warmUp: mobilityA,
    finisher: withFinisher ? stairFinisher : null,
  });
}

/** Warm-up done and two bench sets ticked. */
export function midGymSession(): GymSessionState {
  let state = freshGymSession();
  state = tickDrill(tickDrill(state, 0), 1);
  state = toggleSet(toggleSet(state, 0, 0), 0, 1);
  return state;
}

/** The same session with the stair finisher track (prescription and "N more sessions to step up") as the finisher. */
export function trackFinisherGymSession(): GymSessionState {
  return createGymSession({
    clientSessionId: GYM_CLIENT_SESSION,
    startedAt: GYM_STARTED_AT,
    title: 'Cut to 85 · W3 Upper A',
    plan: upperAPlan,
    warmUp: mobilityA,
    finisher: null,
    finisherTrack: stairTrack,
  });
}

const kgSets = (count: number, kg: number, reps: number) =>
  Array.from({ length: count }, (_, i) => ({ setNumber: i + 1, weightKg: kg, weightLbs: Math.round(kg * 2.20462), originalUnit: 'Kilograms', targetReps: reps, isAmrap: false }));

/** Plan v3 Upper A: bench without a training max yet, two supersets (2a/2b, 3a/3b), an extra set on the left. */
export const planV3UpperA: WeekPlanDto = {
  workoutId: 'w-v3',
  workoutName: 'Plan v3',
  weekNumber: 1,
  dayNumber: 1,
  blockNumber: 1,
  isDeloadWeek: false,
  intensityPercentage: 70,
  exercises: [
    {
      exerciseId: 'v3-bench',
      name: 'Bench Press (Barbell)',
      category: 'MainLift',
      equipment: 'Barbell',
      orderInDay: 1,
      hevyExerciseTemplateId: '79D0BB3A',
      progressionType: 'Linear',
      restSeconds: 180,
      supersetGroup: null,
      extraSetSide: 'None',
      plannedSets: [1, 2, 3, 4, 5].map((n) => ({ setNumber: n, weightKg: 0, weightLbs: 0, originalUnit: 'Kilograms', targetReps: n === 5 ? 10 : 5, isAmrap: n === 5 })),
      metadata: { isTrainingMaxPending: true, intensityPercentage: 0.7, tier: 'Primary' },
    },
    {
      exerciseId: 'v3-row',
      name: 'Bent Over Row (Barbell)',
      category: 'Accessory',
      equipment: 'Barbell',
      orderInDay: 2,
      hevyExerciseTemplateId: '55E6546F',
      progressionType: 'RepsPerSet',
      restSeconds: 60,
      supersetGroup: 2,
      extraSetSide: 'None',
      plannedSets: kgSets(3, 50, 8),
      metadata: {},
    },
    {
      exerciseId: 'v3-lateral',
      name: 'Single Arm Lateral Raise (Cable)',
      category: 'Accessory',
      equipment: 'Cable',
      orderInDay: 3,
      hevyExerciseTemplateId: 'custom:single-arm-lateral',
      progressionType: 'RepsPerSet',
      restSeconds: 60,
      supersetGroup: 2,
      extraSetSide: 'None',
      plannedSets: kgSets(2, 5, 12),
      metadata: { isUnilateral: true },
    },
    {
      exerciseId: 'v3-cable-row',
      name: 'Single Arm Cable Row',
      category: 'Accessory',
      equipment: 'Cable',
      orderInDay: 4,
      hevyExerciseTemplateId: 'custom:single-arm-cable-row',
      progressionType: 'RepsPerSet',
      restSeconds: 60,
      supersetGroup: 3,
      extraSetSide: 'Left',
      plannedSets: kgSets(2, 25, 10),
      metadata: { isUnilateral: true },
    },
    {
      exerciseId: 'v3-skull',
      name: 'Skullcrusher (Dumbbell)',
      category: 'Accessory',
      equipment: 'Dumbbell',
      orderInDay: 5,
      hevyExerciseTemplateId: 'custom:skull-db',
      progressionType: 'RepsPerSet',
      restSeconds: 60,
      supersetGroup: 3,
      extraSetSide: 'None',
      plannedSets: kgSets(2, 12, 10),
      metadata: {},
    },
    {
      exerciseId: 'v3-palloff',
      name: 'Cable Core Palloff Press',
      category: 'Accessory',
      equipment: 'Cable',
      orderInDay: 6,
      hevyExerciseTemplateId: 'custom:palloff',
      progressionType: 'RepsPerSet',
      restSeconds: 90,
      supersetGroup: null,
      extraSetSide: 'None',
      plannedSets: kgSets(2, 15, 10),
      metadata: {},
    },
  ],
};

export const treadmillCoolDown = { kind: 'Treadmill' as const, minutes: 10, notes: 'Easy incline' };

/** Plan v3 Upper A in gym mode: no warm-up drills, a fixed treadmill cool-down. */
export function planV3GymSession(): GymSessionState {
  return createGymSession({
    clientSessionId: GYM_CLIENT_SESSION,
    startedAt: GYM_STARTED_AT,
    title: 'Plan v3 · W1 Upper A',
    plan: planV3UpperA,
    warmUp: null,
    finisher: null,
    finisherFixed: treadmillCoolDown,
  });
}

type PlannedExercise = WeekPlanDto['exercises'][number];

/** Stair climber, Stage 1: 3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3), 11 min. */
export const stairStage1Block: PlannedCardioBlockDto = {
  lane: 'Hard',
  stageName: 'Continuous',
  stageNumber: 1,
  stageCount: 3,
  warmUp: { minutes: 3, level: 3, zone: null },
  coolDown: { minutes: 2, level: 3, zone: null },
  kind: 'Continuous',
  workMinutes: 6,
  intervals: null,
  recoveryLevel: null,
  intensityUnit: 'Level',
  intensity: 6,
  suggestedIntensity: null,
  speed: null,
  isSettingsPending: false,
  totalMinutes: 11,
  text: '3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)',
};

/** Stair climber, Stage 2 at its top (4 × 4/3) with the hard level not chosen yet: level 8 suggested. */
export const stairStage2PendingBlock: PlannedCardioBlockDto = {
  ...stairStage1Block,
  stageName: 'Intervals',
  stageNumber: 2,
  kind: 'Intervals',
  workMinutes: 25,
  intervals: { rounds: 4, hardMinutes: 4, easyMinutes: 3 },
  recoveryLevel: 3,
  intensity: null,
  suggestedIntensity: 8,
  isSettingsPending: true,
  totalMinutes: 30,
  text: '3 min easy (L3) → 4 × (4 min hard / 3 min easy @ L3) → 2 min easy (L3)',
};

/** Incline walk, Stage 1, before its first session: speed and incline still to set. */
export const walkPendingBlock: PlannedCardioBlockDto = {
  lane: 'Easy',
  stageName: 'Build time',
  stageNumber: 1,
  stageCount: 3,
  warmUp: null,
  coolDown: null,
  kind: 'Continuous',
  workMinutes: 10,
  intervals: null,
  recoveryLevel: null,
  intensityUnit: 'InclinePercent',
  intensity: null,
  suggestedIntensity: null,
  speed: null,
  isSettingsPending: true,
  totalMinutes: 10,
  text: '10 min walk',
};

export const walkBlock: PlannedCardioBlockDto = {
  ...walkPendingBlock,
  intensity: 5,
  speed: 5.5,
  isSettingsPending: false,
  text: '10 min @ 5 % incline · 5.5 km/h',
};

function cardioExercise(exerciseId: string, name: string, hevyId: string, orderInDay: number, block: PlannedCardioBlockDto): PlannedExercise {
  return {
    exerciseId,
    name,
    category: 'Cardio',
    equipment: 'Machine',
    orderInDay,
    hevyExerciseTemplateId: hevyId,
    progressionType: 'CardioStage',
    restSeconds: 0,
    supersetGroup: null,
    extraSetSide: 'None',
    plannedSets: [],
    metadata: { isCardioSettingsPending: block.isSettingsPending },
    cardioBlock: block,
  };
}

export const stairExercise = (block: PlannedCardioBlockDto = stairStage1Block) => cardioExercise('v3-stairs', 'Stair Machine (Floors)', '4377A52B', 7, block);
export const walkExercise = (block: PlannedCardioBlockDto = walkBlock) => cardioExercise('v3-walk', 'Treadmill', '243710DE', 8, block);

/** Plan v3 Upper A ending with its cardio exercises (the walk is here too, to test both lanes on one day). */
export function planV3WithCardio(stairs: PlannedCardioBlockDto = stairStage1Block, walk: PlannedCardioBlockDto = walkBlock): WeekPlanDto {
  return { ...planV3UpperA, exercises: [stairExercise(stairs), ...planV3UpperA.exercises, walkExercise(walk)] };
}

export function cardioGymSession(stairs?: PlannedCardioBlockDto, walk?: PlannedCardioBlockDto): GymSessionState {
  return createGymSession({
    clientSessionId: GYM_CLIENT_SESSION,
    startedAt: GYM_STARTED_AT,
    title: 'Plan v3 · W1 Upper A',
    plan: planV3WithCardio(stairs, walk),
    warmUp: null,
    finisher: null,
  });
}
