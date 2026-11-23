import { beforeEach, describe, expect, it } from 'vitest';
import { memoryStorage } from './testStorage';
import { buildFinisherPlan } from './finisherPlan';
import { adjust, back, loadEngine, pause, resume, saveEngine, scheduleOf, skip, startEngine, summaryOf, viewOf } from './finisherEngine';

const T0 = Date.parse('2026-11-23T18:00:00.000Z');
const s = (seconds: number) => T0 + seconds * 1000;
// 3 min warm-up @L3, 3 rounds 1:1 @L6 / easy, 3 min cool-down.
const phases = buildFinisherPlan({
  warmUp: { minutes: 3, level: 3, zone: 'Z2' },
  coolDown: { minutes: 3, level: 3, zone: 'Z2' },
  workMinutes: 6,
  level: 6,
  intervals: { rounds: 3, workSeconds: 60, restSeconds: 60 },
});

describe('finisher engine', () => {
  beforeEach(() => {
    memoryStorage();
  });

  it('derives the phase from the wall clock, even after a long background', () => {
    const state = startEngine('gym-1', phases, T0);

    expect(viewOf(state, s(10))).toMatchObject({ index: 0, remainingMs: 170_000, paused: false });
    // Away on YouTube for 4 min 30 s: warm-up (180) + hard 1 (60) are over, 30 s into easy 1.
    const view = viewOf(state, s(270));
    expect(view.phase).toMatchObject({ kind: 'easy', round: 1 });
    expect(view.remainingMs).toBe(30_000);
    expect(view.next).toMatchObject({ kind: 'hard', round: 2 });
  });

  it('pauses and resumes without losing time', () => {
    let state = startEngine('gym-1', phases, T0);
    state = pause(state, s(60));
    expect(viewOf(state, s(600))).toMatchObject({ index: 0, remainingMs: 120_000, paused: true });
    state = resume(state, s(600));
    expect(viewOf(state, s(610))).toMatchObject({ index: 0, remainingMs: 110_000 });
    expect(state.pausedTotalMs).toBe(540_000);
  });

  it('skip ends the phase now, back restarts it or goes to the previous one', () => {
    let state = startEngine('gym-1', phases, T0);
    state = skip(state, s(20));
    expect(viewOf(state, s(20))).toMatchObject({ index: 1, remainingMs: 60_000 });
    state = back(state, s(21));
    expect(viewOf(state, s(21))).toMatchObject({ index: 0, remainingMs: 180_000 });
    state = back(state, s(31));
    expect(viewOf(state, s(31))).toMatchObject({ index: 0, remainingMs: 180_000 });
  });

  it('±30 s changes only the current phase and never ends it in the past', () => {
    let state = startEngine('gym-1', phases, T0);
    state = adjust(state, 30, s(0));
    expect(viewOf(state, s(0)).remainingMs).toBe(210_000);
    state = adjust(state, -30, s(0));
    state = adjust(state, -30, s(170));
    // Taking 30 s off with only 10 s left ends the warm-up now.
    expect(viewOf(state, s(170))).toMatchObject({ index: 1, remainingMs: 60_000 });
  });

  it('summarises the time actually done and the work level', () => {
    let state = startEngine('gym-1', phases, T0);
    const end = phases.reduce((a, p) => a + p.seconds, 0);
    const view = viewOf(state, s(end + 5));
    expect(view.finished).toBe(true);
    state = skip(state, s(end + 5));
    expect(summaryOf(state)).toEqual({ totalSeconds: end, workSeconds: 360, level: 6, rounds: 3 });
  });

  it('schedules every phase change ahead as absolute times, and nothing while paused', () => {
    const state = startEngine('gym-1', phases, T0);
    const nudges = scheduleOf(state, s(100));
    expect(nudges[0]).toEqual({ at: '2026-11-23T18:03:00.000Z', body: 'Hard: level 6 for 1 min' });
    expect(nudges[1]).toEqual({ at: '2026-11-23T18:04:00.000Z', body: 'Easy: level 3 for 1 min' });
    expect(nudges.at(-1)).toEqual({ at: '2026-11-23T18:12:00.000Z', body: 'Done: log your floors' });
    expect(scheduleOf(pause(state, s(100)), s(100))).toEqual([]);
  });

  it('survives a reload through localStorage', () => {
    const state = startEngine('gym-reload', phases, T0);
    saveEngine(state);
    const loaded = loadEngine('gym-reload')!;
    expect(viewOf(loaded, s(200)).phase).toMatchObject({ kind: 'hard', round: 1 });
    expect(loadEngine('someone-else')).toBeNull();
  });
});
