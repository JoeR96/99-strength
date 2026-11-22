import { stairTrack } from '@/mocks/fixtures/cardioTracks';
/** Deterministic gym-mode data for tests and stories: Upper A with a bench AMRAP, a seal row, Mobility A and a stair finisher. */

import type { WeekPlanDto } from '@/api/workouts';
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
