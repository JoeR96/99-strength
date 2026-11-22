import { describe, it, expect } from 'vitest';
import { DoseUnit, SideBias, type MobilityDrillDto, type MobilityRoutineDto } from '@/types/mobility';
import {
  PREP_SECONDS,
  buildSteps,
  describeDrill,
  drillTicks,
  estimateMinutes,
  initialState,
  playerReducer,
  type PlayerState,
} from './routinePlayer';

const drill = (overrides: Partial<MobilityDrillDto> = {}): MobilityDrillDto => ({
  name: 'Hip flexor stretch',
  sets: 2,
  dose: { unit: DoseUnit.Seconds, amount: 30 },
  extraSetSide: SideBias.None,
  cues: null,
  standingAlternative: null,
  perSide: false,
  ...overrides,
});

const routine = (drills: MobilityDrillDto[]): MobilityRoutineDto => ({ id: 'r1', name: 'Evening reset', targetMinutes: 15, drills });

const run = (state: PlayerState, ...actions: Parameters<typeof playerReducer>[1][]) => actions.reduce(playerReducer, state);
const ticks = (n: number) => Array.from({ length: n }, () => ({ type: 'tick' as const }));

describe('buildSteps', () => {
  it('alternates sides set by set and adds the extra set on the biased side last', () => {
    const steps = buildSteps(routine([drill({ perSide: true, extraSetSide: SideBias.Left })]));

    expect(steps.map((s) => `${s.side}${s.setNumber}/${s.setsOnSide}`)).toEqual(['left1/3', 'right1/2', 'left2/3', 'right2/2', 'left3/3']);
  });

  it('starts on the biased side when it is the right', () => {
    const steps = buildSteps(routine([drill({ perSide: true, sets: 1, extraSetSide: SideBias.Right })]));

    expect(steps.map((s) => s.side)).toEqual(['right', 'left', 'right']);
  });

  it('gives a drill that is not per side its extra set at the end, labelled with the side', () => {
    const steps = buildSteps(routine([drill({ sets: 2, extraSetSide: SideBias.Left })]));

    expect(steps.map((s) => [s.side, s.setNumber, s.setsOnSide])).toEqual([[null, 1, 3], [null, 2, 3], ['left', 3, 3]]);
  });

  it('times breaths at the configured pace and leaves reps untimed', () => {
    const steps = buildSteps(
      routine([drill({ name: 'Breathing', sets: 1, dose: { unit: DoseUnit.Breaths, amount: 6 } }), drill({ name: 'Cat-cow', sets: 1, dose: { unit: DoseUnit.Reps, amount: 8 } })]),
      { secondsPerBreath: 6 }
    );

    expect(steps.map((s) => s.seconds)).toEqual([36, null]);
  });
});

describe('estimateMinutes', () => {
  it('adds the get-ready gaps and rounds up', () => {
    // 4 steps × (5 s prep + 30 s) = 140 s → 3 min
    expect(estimateMinutes(routine([drill({ perSide: true })]))).toBe(3);
  });
});

describe('playerReducer', () => {
  const timed = routine([drill({ name: 'A', sets: 1, dose: { unit: DoseUnit.Seconds, amount: 3 } }), drill({ name: 'B', sets: 1, dose: { unit: DoseUnit.Reps, amount: 8 } })]);

  it('counts down the get-ready gap, then the hold, then moves on by itself', () => {
    let state = initialState(buildSteps(timed));
    expect(state).toMatchObject({ index: 0, phase: 'prep', remaining: PREP_SECONDS });

    state = run(state, ...ticks(PREP_SECONDS));
    expect(state).toMatchObject({ index: 0, phase: 'work', remaining: 3 });

    state = run(state, ...ticks(3));
    expect(state).toMatchObject({ index: 1, phase: 'prep' });
    expect(state.done).toEqual([true, false]);
  });

  it('waits on a rep step until Done is tapped', () => {
    let state = run(initialState(buildSteps(timed)), { type: 'skip' }, ...ticks(PREP_SECONDS));
    expect(state).toMatchObject({ index: 1, phase: 'work', remaining: null });

    state = run(state, ...ticks(30));
    expect(state.phase).toBe('work');

    state = run(state, { type: 'done' });
    expect(state.phase).toBe('finished');
  });

  it('does not count while paused', () => {
    const state = run(initialState(buildSteps(timed)), { type: 'pause' }, ...ticks(10));

    expect(state).toMatchObject({ phase: 'prep', remaining: PREP_SECONDS, paused: true });
  });

  it('goes back to the previous step and never before the first', () => {
    const state = run(initialState(buildSteps(timed)), { type: 'back' }, { type: 'skip' }, { type: 'back' });

    expect(state).toMatchObject({ index: 0, phase: 'prep' });
  });

  it('ticks a drill when any of its steps was completed, not when all were skipped', () => {
    const state = run(initialState(buildSteps(timed)), { type: 'skip' }, { type: 'done' });

    expect(state.phase).toBe('finished');
    expect(drillTicks(state, 2)).toEqual([false, true]);
  });

  it('finishes at once for an empty routine', () => {
    expect(initialState([]).phase).toBe('finished');
  });
});

describe('describeDrill', () => {
  it('reads like the plan', () => {
    expect(describeDrill(drill({ perSide: true, extraSetSide: SideBias.Left }))).toBe('2 × 30 s per side, extra set left');
    expect(describeDrill(drill({ sets: 2, dose: { unit: DoseUnit.Breaths, amount: 5 } }))).toBe('2 × 5 breaths');
  });
});
