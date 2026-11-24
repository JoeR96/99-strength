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
  /**
   * The blank settings were chosen on this phone (no signal for the save): `block` carries them, and the finish
   * sends them with the result, where the server sets them first.
   */
  settingsChosen?: boolean;
}

export type GymCardioChange = Partial<Pick<GymCardio, 'finished' | 'pain' | 'totalSeconds' | 'workMinutes' | 'intensity' | 'speed' | 'floors'>>;

/** A cardio block in one line for lists and previews: the block's text, or what to set first. */
export function cardioBlockLine(block: PlannedCardioBlockDto): string {
  if (!block.isSettingsPending) return block.text;
  return block.lane === 'Hard' ? 'Set the hard level first' : 'Set speed and incline first';
}

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

/**
 * Red readiness skips the hard lane (the walk always shows). Never skips an item already logged, nor
 * one kept on this phone ("Do it anyway", `keepHardCardio`).
 */
export function setHardCardioSkipped(state: GymSessionState, skip: boolean): GymSessionState {
  const target = (c: GymCardio) => skip && !state.hardCardioKept && !c.done;
  if (!state.cardio.some((c) => c.lane === 'Hard' && (c.skipped ?? false) !== target(c))) return state;
  return { ...state, cardio: state.cardio.map((c) => (c.lane === 'Hard' ? { ...c, skipped: target(c) } : c)) };
}

/** "Do it anyway": un-skips the hard lane and remembers it in the draft, so it works offline and after a reload. */
export function keepHardCardio(state: GymSessionState): GymSessionState {
  return setHardCardioSkipped({ ...state, hardCardioKept: true }, false);
}

const formatNumber = (n: number) => String(Math.round(n * 10) / 10);

/**
 * Starting values chosen on the phone (the walk's speed and incline, or the hard level): the item is unblocked at once
 * and the finish carries them, so it works with no signal. The block shows them until the plan comes back with its own.
 */
export function chooseCardioSettings(state: GymSessionState, index: number, values: { intensity: number; speed?: number | null }): GymSessionState {
  return updateItem(state, index, (c) => {
    const walk = c.block.intensityUnit === 'InclinePercent';
    const speed = walk ? (values.speed ?? c.block.speed) : c.block.speed;
    const text = walk && speed != null ? `${c.block.workMinutes} min @ ${formatNumber(values.intensity)} % incline, ${formatNumber(speed)} km/h` : c.block.text;
    return {
      ...c,
      intensity: values.intensity,
      speed,
      settingsChosen: true,
      block: { ...c.block, intensity: values.intensity, speed, suggestedIntensity: null, isSettingsPending: false, text },
    };
  });
}

export interface SyncCardioOptions {
  /** The plan slot still has a finisher; false drops one an old draft kept; undefined (not known yet) leaves it. */
  slotHasFinisher?: boolean;
}

/**
 * Brings a draft up to the week plan: items not done yet take a new block (settings saved), cardio exercises the draft
 * does not have are added in plan order (a draft started on an older app), and a finisher the plan slot no longer has
 * is dropped. Settings chosen on the phone are kept while the plan still has them blank.
 */
export function syncCardioBlocks(state: GymSessionState, plan: WeekPlanDto, options: SyncCardioOptions = {}): GymSessionState {
  let changed = false;
  const planned = [...plan.exercises].sort((a, b) => a.orderInDay - b.orderInDay).filter((e) => e.cardioBlock);
  const updated = (c: GymCardio): GymCardio => {
    if (c.done) return c;
    const block = plan.exercises.find((e) => e.exerciseId === c.exerciseId)?.cardioBlock;
    if (!block || JSON.stringify(block) === JSON.stringify(c.block)) return c;
    if (c.settingsChosen && block.isSettingsPending) return c;
    changed = true;
    const { settingsChosen: _chosen, ...rest } = c;
    void _chosen;
    return { ...rest, block, intensity: block.intensity, speed: block.speed };
  };
  const fromPlan = planned.map((e) => {
    const existing = state.cardio.find((c) => c.exerciseId === e.exerciseId);
    if (existing) return updated(existing);
    changed = true;
    return cardioFromPlan({ ...e, cardioBlock: e.cardioBlock! });
  });
  const others = state.cardio.filter((c) => !planned.some((e) => e.exerciseId === c.exerciseId)).map(updated);
  const cardio = [...fromPlan, ...others];
  if (cardio.some((c, i) => c !== state.cardio[i])) changed = true;
  const dropFinisher = options.slotHasFinisher === false && state.finisher !== null;
  if (!changed && !dropFinisher) return state;
  return { ...state, cardio: changed ? cardio : state.cardio, finisher: dropFinisher ? null : state.finisher };
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
