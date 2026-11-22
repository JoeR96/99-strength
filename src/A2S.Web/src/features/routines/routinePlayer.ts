/**
 * Pure model of the home routine player: a routine expanded into steps (one per set, per side),
 * and a reducer that walks them with a short "get ready" gap before each step. Timed steps
 * (seconds, breaths) count down and move on by themselves; rep steps wait for a tap on Done.
 * No timers or audio here: the page drives `tick` once a second and plays sounds on transitions.
 */

import { DoseUnit, SideBias, type DrillLevelDto, type MobilityDrillDto, type MobilityRoutineDto } from '@/types/mobility';

export type Side = 'left' | 'right' | null;

export interface PlayerStep {
  drillIndex: number;
  drillName: string;
  /** 1-based set number on this side. */
  setNumber: number;
  /** Sets on this side, including an extra set when the bias is this side. */
  setsOnSide: number;
  side: Side;
  unit: DoseUnit;
  amount: number;
  /** Countdown length, or null for rep-based steps that end on a tap. */
  seconds: number | null;
  cues: string | null;
  standingAlternative: string | null;
  /** The drill's current level ("Level 2"), when it has a progression chain. */
  levelName: string | null;
}

export interface PlayerOptions {
  /** Seconds a breath-based dose counts as. */
  secondsPerBreath?: number;
}

export const PREP_SECONDS = 5;
/** Rough seconds per rep, for the time estimate only. */
const SECONDS_PER_REP = 3;

function biasSide(bias: SideBias): Side {
  if (bias === SideBias.Left) return 'left';
  if (bias === SideBias.Right) return 'right';
  return null;
}

function stepSeconds(drill: MobilityDrillDto, secondsPerBreath: number): number | null {
  if (drill.dose.unit === DoseUnit.Seconds) return drill.dose.amount;
  if (drill.dose.unit === DoseUnit.Breaths) return drill.dose.amount * secondsPerBreath;
  return null;
}

/** The level a drill is on now, or null when it has no progression chain. */
export function currentLevel(drill: MobilityDrillDto): DrillLevelDto | null {
  const levels = drill.levels ?? [];
  return levels.length ? levels[Math.min(drill.currentLevel ?? 0, levels.length - 1)] : null;
}

/**
 * Per-side drills alternate sides set by set, always left side first, and finish with any (legacy)
 * extra set on the biased side. A drill that isn't per side gets
 * its extra set at the end, labelled with the biased side.
 */
export function buildSteps(routine: MobilityRoutineDto, options: PlayerOptions = {}): PlayerStep[] {
  const secondsPerBreath = options.secondsPerBreath ?? 5;
  const steps: PlayerStep[] = [];

  routine.drills.forEach((drill, drillIndex) => {
    const extra = biasSide(drill.extraSetSide);
    const seconds = stepSeconds(drill, secondsPerBreath);
    const base = {
      drillIndex,
      drillName: drill.name,
      unit: drill.dose.unit,
      amount: drill.dose.amount,
      seconds,
      cues: currentLevel(drill)?.cues ?? drill.cues,
      standingAlternative: drill.standingAlternative,
      levelName: currentLevel(drill)?.name ?? null,
    };

    if (drill.perSide) {
      const first: Side = 'left';
      const second: Side = first === 'left' ? 'right' : 'left';
      const setsOn = (side: Side) => drill.sets + (extra === side ? 1 : 0);
      for (let set = 1; set <= drill.sets; set++) {
        steps.push({ ...base, setNumber: set, setsOnSide: setsOn(first), side: first });
        steps.push({ ...base, setNumber: set, setsOnSide: setsOn(second), side: second });
      }
      if (extra) steps.push({ ...base, setNumber: drill.sets + 1, setsOnSide: setsOn(extra), side: extra });
    } else {
      const total = drill.sets + (extra ? 1 : 0);
      for (let set = 1; set <= total; set++) {
        steps.push({ ...base, setNumber: set, setsOnSide: total, side: set > drill.sets ? extra : null });
      }
    }
  });

  return steps;
}

/** Whole minutes the routine takes, including the get-ready gaps; at least 1. */
export function estimateMinutes(routine: MobilityRoutineDto, options: PlayerOptions = {}): number {
  const total = buildSteps(routine, options).reduce(
    (sum, step) => sum + PREP_SECONDS + (step.seconds ?? step.amount * SECONDS_PER_REP),
    0
  );
  return Math.max(1, Math.ceil(total / 60));
}

export type Phase = 'prep' | 'work' | 'finished';

export interface PlayerState {
  steps: PlayerStep[];
  index: number;
  phase: Phase;
  /** Seconds left in the current phase; null while a rep step waits for Done. */
  remaining: number | null;
  paused: boolean;
  done: boolean[];
}

export type PlayerAction =
  | { type: 'tick' }
  | { type: 'done' }
  | { type: 'skip' }
  | { type: 'back' }
  | { type: 'pause' }
  | { type: 'resume' };

export function initialState(steps: PlayerStep[]): PlayerState {
  return {
    steps,
    index: 0,
    phase: steps.length === 0 ? 'finished' : 'prep',
    remaining: steps.length === 0 ? null : PREP_SECONDS,
    paused: false,
    done: steps.map(() => false),
  };
}

function goTo(state: PlayerState, index: number): PlayerState {
  if (index >= state.steps.length) return { ...state, index: state.steps.length, phase: 'finished', remaining: null, paused: false };
  return { ...state, index, phase: 'prep', remaining: PREP_SECONDS };
}

function markDone(state: PlayerState): PlayerState {
  const done = state.done.slice();
  done[state.index] = true;
  return { ...state, done };
}

export function playerReducer(state: PlayerState, action: PlayerAction): PlayerState {
  if (state.phase === 'finished') return state;

  switch (action.type) {
    case 'tick': {
      if (state.paused || state.remaining === null) return state;
      if (state.remaining > 1) return { ...state, remaining: state.remaining - 1 };
      if (state.phase === 'prep') return { ...state, phase: 'work', remaining: state.steps[state.index].seconds };
      return goTo(markDone(state), state.index + 1);
    }
    case 'done':
      return goTo(markDone(state), state.index + 1);
    case 'skip':
      return goTo(state, state.index + 1);
    case 'back':
      return goTo(state, Math.max(0, state.index - 1));
    case 'pause':
      return { ...state, paused: true };
    case 'resume':
      return { ...state, paused: false };
  }
}

/** One tick per drill: true when at least one of its steps was completed (not skipped). */
export function drillTicks(state: PlayerState, drillCount: number): boolean[] {
  const ticks = Array.from({ length: drillCount }, () => false);
  state.steps.forEach((step, i) => {
    if (state.done[i]) ticks[step.drillIndex] = true;
  });
  return ticks;
}

/** "5 breaths", "30 seconds", "1 rep". */
export function formatDose(unit: DoseUnit, amount: number): string {
  const word = { [DoseUnit.Reps]: ['rep', 'reps'], [DoseUnit.Seconds]: ['second', 'seconds'], [DoseUnit.Breaths]: ['breath', 'breaths'] }[unit];
  return `${amount} ${amount === 1 ? word[0] : word[1]}`;
}

/** "2 × 30 s per side, left side first". */
export function describeDrill(drill: MobilityDrillDto): string {
  const unit = { [DoseUnit.Reps]: '', [DoseUnit.Seconds]: ' s', [DoseUnit.Breaths]: ' breaths' }[drill.dose.unit];
  const parts = [`${drill.sets} × ${drill.dose.amount}${unit}${drill.perSide ? ' per side' : ''}`];
  if (drill.perSide) parts.push('left side first');
  if (drill.extraSetSide === SideBias.Left) parts.push('extra set left');
  if (drill.extraSetSide === SideBias.Right) parts.push('extra set right');
  return parts.join(', ');
}
