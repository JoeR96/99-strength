/**
 * Gym mode's cardio exercises (Plan v3: the stair climber is the hard lane, the incline walk the easy
 * lane). Each is one item after the lifts: its block (warm-up → work → cool-down) from the week plan,
 * then how it went. An item is done once Easy / Solid / Brutal is chosen, and only done items are sent,
 * so a cardio exercise left alone keeps its progression. Pure steps, like `gymSession.ts`.
 */

import type { PlannedCardioBlockDto, PlannedExerciseDto, WeekPlanDto } from '@/api/workouts';
import type { Effort } from '@/types/gym';
import type { ExercisePerformanceRequest } from '@/types/workoutDtos';
import type { GymSessionState } from './gymSession';

export interface GymCardio {
  exerciseId: string;
  name: string;
  lane: 'Hard' | 'Easy';
  block: PlannedCardioBlockDto;
  /** An effort was chosen: the item is sent on Finish. */
  done: boolean;
  /** False: "Stopped early". */
  finished: boolean;
  effort: Effort | null;
  pain?: 'None' | 'Niggle';
  /** The whole session (wizard summary, or the block's minutes). For the walk, set by its tick. */
  totalSeconds?: number;
  workMinutes?: number;
  /** Level (stairs) or incline % (walk), from the block. */
  intensity?: number | null;
  speed?: number | null;
  floors?: number | null;
  /** Red readiness: the hard lane is skipped for recovery (not sent) unless kept. */
  skipped?: boolean;
}

export type GymCardioChange = Partial<Pick<GymCardio, 'finished' | 'pain' | 'totalSeconds' | 'workMinutes' | 'intensity' | 'speed' | 'floors'>>;

export function cardioFromPlan(e: PlannedExerciseDto & { cardioBlock: PlannedCardioBlockDto }): GymCardio {
  const block = e.cardioBlock;
  return {
    exerciseId: e.exerciseId,
    name: e.name,
    lane: block.lane,
    block,
    done: false,
    finished: true,
    effort: null,
    pain: 'None',
    intensity: block.intensity,
    speed: block.speed,
  };
}

function updateItem(state: GymSessionState, index: number, change: (c: GymCardio) => GymCardio): GymSessionState {
  return { ...state, cardio: state.cardio.map((c, i) => (i === index ? change(c) : c)) };
}

const blockSeconds = (c: GymCardio) => c.block.totalMinutes * 60;

/** Easy / Solid / Brutal: the item is done; the session's length defaults to the block's. */
export function chooseCardioEffort(state: GymSessionState, index: number, effort: Effort): GymSessionState {
  return updateItem(state, index, (c) => ({ ...c, effort, done: true, totalSeconds: c.totalSeconds || blockSeconds(c) }));
}

export function updateCardio(state: GymSessionState, index: number, change: GymCardioChange): GymSessionState {
  return updateItem(state, index, (c) => ({ ...c, ...change }));
}

/** The walk's tick: done as prescribed (then it asks how it felt); a second tap clears it. */
export function tickCardio(state: GymSessionState, index: number): GymSessionState {
  return updateItem(state, index, (c) => {
    if (c.totalSeconds === undefined) return { ...c, totalSeconds: blockSeconds(c) };
    const { totalSeconds: _cleared, ...rest } = c;
    void _cleared;
    return { ...rest, effort: null, done: false };
  });
}

/** Red readiness skips the hard lane (the walk always shows); keeping it brings it back. */
export function setHardCardioSkipped(state: GymSessionState, skipped: boolean): GymSessionState {
  if (!state.cardio.some((c) => c.lane === 'Hard' && (c.skipped ?? false) !== skipped)) return state;
  return { ...state, cardio: state.cardio.map((c) => (c.lane === 'Hard' ? { ...c, skipped } : c)) };
}

/** After settings are saved the plan comes back with the new block: items not done yet take it. */
export function syncCardioBlocks(state: GymSessionState, plan: WeekPlanDto): GymSessionState {
  let changed = false;
  const cardio = state.cardio.map((c) => {
    if (c.done) return c;
    const block = plan.exercises.find((e) => e.exerciseId === c.exerciseId)?.cardioBlock;
    if (!block || JSON.stringify(block) === JSON.stringify(c.block)) return c;
    changed = true;
    return { ...c, block, intensity: block.intensity, speed: block.speed };
  });
  return changed ? { ...state, cardio } : state;
}

export function isCardioDone(c: GymCardio): boolean {
  return c.done && !c.skipped && c.effort !== null;
}

/** One performance per done item, with no sets. */
export function cardioPerformances(state: GymSessionState): ExercisePerformanceRequest[] {
  return state.cardio.filter(isCardioDone).map((c) => ({
    exerciseId: c.exerciseId,
    cardio: {
      finished: c.finished,
      effort: c.effort!,
      pain: c.pain ?? 'None',
      totalSeconds: Math.round(c.totalSeconds || blockSeconds(c)),
      ...(c.workMinutes != null ? { workMinutes: c.workMinutes } : {}),
      ...(c.intensity != null ? { intensity: c.intensity } : {}),
      ...(c.speed != null ? { speed: c.speed } : {}),
      ...(c.floors != null ? { floors: c.floors } : {}),
    },
  }));
}
