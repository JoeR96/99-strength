import { describe, it, expect, beforeEach, vi } from 'vitest';
import { WeightUnit } from '@/types/workout';
import {
  allDrillsDone,
  clearGymSession,
  editSet,
  loadGymSession,
  newClientSessionId,
  progressOf,
  restSecondsFor,
  saveGymSession,
  toFinishRequest,
  toggleSet,
  updateFinisher,
} from './gymSession';
import { freshGymSession, GYM_CLIENT_SESSION, GYM_STARTED_AT, midGymSession, trackFinisherGymSession } from './gymFixtures';

const ENDED_AT = '2026-11-16T18:58:00.000Z';

describe('createGymSession', () => {
  it('orders the checklist warm-up → lifts → finisher with the planned sets pre-filled', () => {
    const state = freshGymSession();

    expect(state.drills.map((d) => [d.name, d.dose])).toEqual([
      ['Open Books', '1 × 8 reps each side'],
      ['Dead Hang', '2 × 30s'],
    ]);
    expect(state.lifts.map((l) => [l.name, l.restSeconds, l.sets.length])).toEqual([
      ['Bench Press (Barbell)', 180, 5],
      ['Seal Row (Barbell)', 90, 3],
    ]);
    expect(state.lifts[0].sets[4]).toMatchObject({ weight: 75, reps: 5, isAmrap: true, done: false });
    expect(state.finisher).toMatchObject({ name: 'Stair finisher', minutes: 10, effort: null });
  });

  it('falls back to 180 s for main lifts and 90 s for the rest', () => {
    expect(restSecondsFor('MainLift', undefined)).toBe(180);
    expect(restSecondsFor('Accessory', 0)).toBe(90);
    expect(restSecondsFor('Accessory', 120)).toBe(120);
  });
});

describe('sets', () => {
  it('confirms a set as planned, and a second tap undoes it', () => {
    const once = toggleSet(freshGymSession(), 0, 0);
    const twice = toggleSet(once, 0, 0);

    expect(once.lifts[0].sets[0].done).toBe(true);
    expect(twice.lifts[0].sets[0].done).toBe(false);
  });

  it('records the AMRAP reps given by the prompt', () => {
    const state = toggleSet(freshGymSession(), 0, 4, 9);

    expect(state.lifts[0].sets[4]).toMatchObject({ done: true, reps: 9 });
  });

  it('edits weight and reps within bounds', () => {
    const state = editSet(editSet(freshGymSession(), 1, 0, { weight: 42.5 }), 1, 0, { reps: -3 });

    expect(state.lifts[1].sets[0]).toMatchObject({ weight: 42.5, reps: 0 });
  });
});

describe('progressOf', () => {
  it('can finish after one set and is complete only when everything is logged', () => {
    const mid = progressOf(midGymSession());
    let all = allDrillsDone(freshGymSession());
    all.lifts.forEach((lift, li) => lift.sets.forEach((_, si) => (all = toggleSet(all, li, si))));
    const withoutFinisher = progressOf(all);
    const done = progressOf(updateFinisher(all, { minutes: 10, effort: 'Solid' }));

    expect(mid).toMatchObject({ setsDone: 2, setsTotal: 8, drillsDone: 2, canFinish: true, complete: false });
    expect(progressOf(freshGymSession()).canFinish).toBe(false);
    expect(withoutFinisher.complete).toBe(false);
    expect(done.complete).toBe(true);
  });
});

describe('toFinishRequest', () => {
  it('sends only confirmed sets and leaves untouched lifts out (Finish early)', () => {
    const request = toFinishRequest(toggleSet(midGymSession(), 0, 4, 11), ENDED_AT);

    expect(request).toMatchObject({ clientSessionId: GYM_CLIENT_SESSION, workoutId: 'w-1', day: 1, startedAt: GYM_STARTED_AT, endedAt: ENDED_AT });
    expect(request.warmUpTicks).toEqual([true, true]);
    expect(request.performances).toHaveLength(1);
    expect(request.performances[0].exerciseId).toBe('ex-bench');
    expect(request.performances[0].completedSets).toEqual([
      { setNumber: 1, weight: 75, weightUnit: WeightUnit.Kilograms, actualReps: 5, wasAmrap: false },
      { setNumber: 2, weight: 75, weightUnit: WeightUnit.Kilograms, actualReps: 5, wasAmrap: false },
      { setNumber: 5, weight: 75, weightUnit: WeightUnit.Kilograms, actualReps: 11, wasAmrap: true },
    ]);
    expect(request.finisher).toBeNull();
  });

  it('includes the finisher once minutes and effort are logged', () => {
    const state = updateFinisher(midGymSession(), { minutes: 12.5, level: 8, floors: 41, effort: 'Brutal' });

    expect(toFinishRequest(state, ENDED_AT).finisher).toEqual({ durationSeconds: 750, effort: 'Brutal', level: 8, floors: 41 });
  });

  it('uses a track finisher: its prescription, level and step-up line, and sends a pain flag', () => {
    const state = trackFinisherGymSession();

    expect(state.finisher).toMatchObject({
      trackId: '6a1e0000-0000-4000-8000-000000000003',
      name: 'Stair finisher',
      prescription: '7 min continuous @ level 6',
      stepUp: '2 more sessions to step up',
      minutes: 7,
      level: 6,
    });
    const logged = updateFinisher(state, { floors: 30, effort: 'Brutal', pain: 'Niggle' });
    expect(toFinishRequest(logged, ENDED_AT).finisher).toEqual({ durationSeconds: 420, effort: 'Brutal', level: 6, floors: 30, pain: 'Niggle' });
  });

  it('never ends before it started', () => {
    expect(toFinishRequest(midGymSession(), '2026-11-16T17:00:00.000Z').endedAt).toBe(GYM_STARTED_AT);
  });
});

describe('local save', () => {
  // The test setup mocks localStorage with spies; back them with a real map here.
  beforeEach(() => {
    const store = new Map<string, string>();
    vi.mocked(localStorage.getItem).mockImplementation((key: string) => store.get(key) ?? null);
    vi.mocked(localStorage.setItem).mockImplementation((key: string, value: string) => void store.set(key, value));
    vi.mocked(localStorage.removeItem).mockImplementation((key: string) => void store.delete(key));
  });

  it('round-trips the session and clears it after finishing', () => {
    const state = midGymSession();
    saveGymSession(state);

    expect(loadGymSession('w-1', 3, 1)).toEqual(state);
    clearGymSession('w-1', 3, 1);
    expect(loadGymSession('w-1', 3, 1)).toBeNull();
  });

  it('ignores something unreadable', () => {
    localStorage.setItem('a2s-gym-session:w-1:3:1', '{not json');

    expect(loadGymSession('w-1', 3, 1)).toBeNull();
  });
});

describe('newClientSessionId', () => {
  it('is a v4 uuid', () => {
    expect(newClientSessionId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });
});
