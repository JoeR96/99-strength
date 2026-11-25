import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { WeightUnit } from '@/types/workout';
import {
  allDrillsDone,
  clearGymSession,
  editSet,
  expireGymDrafts,
  GYM_SESSION_VERSION,
  loadGymSession,
  migrateGymSession,
  newClientSessionId,
  progressOf,
  restSecondsFor,
  saveGymSession,
  toFinishRequest,
  sessionTimes,
  startClockOnFirstTick,
  toggleSet,
  updateFinisher,
  liftBlocks,
  restAfterSet,
  toggleExtraSet,
  toggleFixedFinisher,
  syncPendingLifts,
  sideRows,
  toggleSide,
} from './gymSession';
import { freshGymSession, GYM_CLIENT_SESSION, GYM_STARTED_AT, midGymSession, planV3GymSession, planV3UpperA, trackFinisherGymSession } from './gymFixtures';

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
      // Total (3 easy + 7 work + 3 easy), with the work block prefilled beside it.
      minutes: 13,
      workMinutes: 7,
      warmUp: { minutes: 3, level: 3, zone: 'Z2' },
      coolDown: { minutes: 3, level: 3, zone: 'Z1–2' },
      level: 6,
    });
    const logged = updateFinisher(state, { floors: 30, effort: 'Brutal', pain: 'Niggle' });
    expect(toFinishRequest(logged, ENDED_AT).finisher).toEqual({ durationSeconds: 780, effort: 'Brutal', level: 6, floors: 30, workMinutes: 7, pain: 'Niggle' });
  });

  it('never ends before it started', () => {
    expect(toFinishRequest(midGymSession(), '2026-11-16T17:00:00.000Z').endedAt).toBe(GYM_STARTED_AT);
  });
});

let store = new Map<string, string>();

// The test setup mocks localStorage with spies; back them with a real map here.
function backLocalStorageWithAMap() {
  store = new Map<string, string>();
  vi.mocked(localStorage.getItem).mockImplementation((key: string) => store.get(key) ?? null);
  vi.mocked(localStorage.setItem).mockImplementation((key: string, value: string) => void store.set(key, value));
  vi.mocked(localStorage.removeItem).mockImplementation((key: string) => void store.delete(key));
  vi.mocked(localStorage.key).mockImplementation((i: number) => [...store.keys()][i] ?? null);
  Object.defineProperty(localStorage, 'length', { configurable: true, get: () => store.size });
}

describe('local save', () => {
  beforeEach(backLocalStorageWithAMap);

  it('saves the current version', () => {
    saveGymSession(midGymSession());

    expect(JSON.parse(store.get('a2s-gym-session:w-1:3:1')!).version).toBe(GYM_SESSION_VERSION);
  });

  it('upgrades a draft saved before versioning (no version field) and keeps everything logged', () => {
    const { version: _dropped, ...unversioned } = midGymSession();
    void _dropped;
    localStorage.setItem('a2s-gym-session:w-1:3:1', JSON.stringify(unversioned));

    expect(loadGymSession('w-1', 3, 1)).toEqual(midGymSession());
  });

  it('discards a draft it cannot recognise, and only that', () => {
    localStorage.setItem('a2s-gym-session:w-1:3:1', JSON.stringify({ version: 99, clientSessionId: 'x', lifts: [] }));
    localStorage.setItem('a2s-gym-session:w-1:3:2', JSON.stringify({ ...midGymSession(), day: 2 }));

    expect(loadGymSession('w-1', 3, 1)).toBeNull();
    expect(store.has('a2s-gym-session:w-1:3:1')).toBe(false);
    expect(loadGymSession('w-1', 3, 2)?.clientSessionId).toBe(midGymSession().clientSessionId);
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

describe('migrateGymSession', () => {
  it('rejects shapes that are not a session', () => {
    expect(migrateGymSession(null)).toBeNull();
    expect(migrateGymSession([])).toBeNull();
    expect(migrateGymSession({ clientSessionId: 'a', workoutId: 'w', day: '1', week: 3, startedAt: 'x', lifts: [] })).toBeNull();
  });
});

describe('expireGymDrafts', () => {
  const NOW = new Date('2026-11-23T09:00:00.000Z');
  const draft = (workoutId: string, week: number, day: number, startedAt: string) =>
    localStorage.setItem(`a2s-gym-session:${workoutId}:${week}:${day}`, JSON.stringify({ ...midGymSession(), workoutId, week, day, startedAt }));

  beforeEach(() => {
    backLocalStorageWithAMap();
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it('removes drafts started more than 7 days ago and keeps newer ones', () => {
    draft('w-1', 3, 1, '2026-11-16T08:59:00.000Z'); // 7 days and a minute
    draft('w-1', 3, 2, '2026-11-16T09:01:00.000Z'); // just inside 7 days
    localStorage.setItem('other-key', 'kept');

    const removed = expireGymDrafts();

    expect(removed).toEqual(['a2s-gym-session:w-1:3:1']);
    expect([...store.keys()]).toEqual(['a2s-gym-session:w-1:3:2', 'other-key']);
  });

  it("removes a workout's drafts for weeks before the current one, not other workouts'", () => {
    draft('w-1', 2, 1, '2026-11-22T18:00:00.000Z');
    draft('w-1', 3, 1, '2026-11-22T18:00:00.000Z');
    draft('w-2', 1, 1, '2026-11-22T18:00:00.000Z');

    expireGymDrafts(new Date(), { workoutId: 'w-1', currentWeek: 3 });

    expect([...store.keys()]).toEqual(['a2s-gym-session:w-1:3:1', 'a2s-gym-session:w-2:1:1']);
  });

  it('moves with the clock: a draft kept today expires a week later', () => {
    draft('w-1', 3, 1, '2026-11-22T18:00:00.000Z');
    expect(expireGymDrafts()).toEqual([]);

    vi.setSystemTime(new Date('2026-11-30T09:00:00.000Z'));

    expect(expireGymDrafts()).toEqual(['a2s-gym-session:w-1:3:1']);
  });
});

describe('newClientSessionId', () => {
  it('is a v4 uuid', () => {
    expect(newClientSessionId()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });
});

describe('supersets, extra sets, fixed cool-down and pending training max (Plan v3)', () => {
  it('groups superset partners into one block labelled 2a/2b and alternates A1 → B1 → A2 → B2, then the longer one’s rest', () => {
    const state = planV3GymSession();
    const blocks = liftBlocks(state);

    expect(blocks.map((b) => b.labels)).toEqual([[null], ['2a', '2b'], ['3a', '3b'], [null]]);
    const rowBlock = blocks[1];
    expect(rowBlock.order.map((s) => `${state.lifts[s.lift].name.split(' ')[0]}${s.set + 1}`)).toEqual([
      'Bent1',
      'Single1',
      'Bent2',
      'Single2',
      'Bent3',
    ]);
  });

  it('starts no rest between A and B, the superset rest after B, and rest after A’s sets left over', () => {
    let state = planV3GymSession();

    expect(restAfterSet(state, 1, 0)).toBeNull();
    state = toggleSet(state, 1, 0);
    expect(restAfterSet(state, 2, 0)).toBe(60);
    expect(restAfterSet(state, 1, 2)).toBe(60);
    expect(restAfterSet(state, 0, 0)).toBe(180);
  });

  it('rests after A when B’s matching set is already done', () => {
    const state = toggleSet(planV3GymSession(), 2, 0);

    expect(restAfterSet(state, 1, 0)).toBe(60);
  });

  it('sends a ticked extra set as extraSet, not in completedSets', () => {
    let state = planV3GymSession();
    expect(state.lifts[3].extraSet).toEqual({ side: 'Left', weight: 25, reps: 10, done: false });
    state = toggleSet(toggleSet(state, 3, 0), 3, 1);
    state = toggleExtraSet(state, 3);

    const request = toFinishRequest(state, ENDED_AT);
    const row = request.performances.find((p) => p.exerciseId === 'v3-cable-row')!;
    expect(row.completedSets).toHaveLength(2);
    expect(row.extraSet).toEqual({ setNumber: 3, weight: 25, weightUnit: WeightUnit.Kilograms, actualReps: 10, wasAmrap: false });
    expect(progressOf(state).setsTotal).toBe(16);
  });

  it('leaves an unticked extra set out', () => {
    const state = toggleSet(planV3GymSession(), 3, 0);

    expect(toFinishRequest(state, ENDED_AT).performances[0].extraSet).toBeUndefined();
  });

  it('ticks the fixed treadmill cool-down and sends it as done as prescribed', () => {
    let state = toggleSet(planV3GymSession(), 1, 0);
    expect(state.finisher?.prescription).toBe('Treadmill · easy incline · 10 min');
    expect(toFinishRequest(state, ENDED_AT).finisher).toBeNull();

    state = toggleFixedFinisher(state);

    expect(progressOf(state).finisherLogged).toBe(true);
    expect(toFinishRequest(state, ENDED_AT).finisher).toEqual({ durationSeconds: 600, effort: 'Easy', completedAsPrescribed: true });
  });

  it('blocks Finish while a lift without a training max has logged sets, and allows it when that lift is left out', () => {
    const withBench = toggleSet(toggleSet(planV3GymSession(), 1, 0), 0, 0);
    const withoutBench = toggleSet(planV3GymSession(), 1, 0);

    expect(progressOf(withBench)).toMatchObject({ canFinish: false, needsTrainingMax: ['Bench Press (Barbell)'] });
    expect(progressOf(withoutBench)).toMatchObject({ canFinish: true, needsTrainingMax: [] });
    expect(withBench.lifts[0]).toMatchObject({ trainingMaxPending: true, intensity: 0.7 });
  });

  it('fills a pending lift’s weights from the plan once its training max is set', () => {
    const state = toggleSet(planV3GymSession(), 0, 0);
    const plan = {
      ...planV3UpperA,
      exercises: planV3UpperA.exercises.map((e) =>
        e.exerciseId === 'v3-bench'
          ? { ...e, metadata: { ...e.metadata, isTrainingMaxPending: false }, plannedSets: e.plannedSets.map((s) => ({ ...s, weightKg: 70, weightLbs: 155 })) }
          : e
      ),
    };

    const synced = syncPendingLifts(state, plan);

    expect(synced.lifts[0].trainingMaxPending).toBe(false);
    expect(synced.lifts[0].sets[0]).toMatchObject({ done: true, weight: 0, plannedWeight: 70 });
    expect(synced.lifts[0].sets[1]).toMatchObject({ done: false, weight: 70 });
    expect(syncPendingLifts(state, planV3UpperA)).toBe(state);
  });
});

describe('unilateral lifts', () => {
  it('lists each set Left then Right, left side first, and only Right confirms the set', () => {
    let state = planV3GymSession();
    const lateral = state.lifts[2];
    expect(lateral.unilateral).toBe(true);
    expect(sideRows(lateral)).toEqual(['Left', 'Right']);
    expect(sideRows(state.lifts[1])).toEqual([null]);

    state = toggleSide(state, 2, 0, 'Left');
    expect(state.lifts[2].sets[0]).toMatchObject({ leftDone: true, done: false });
    state = toggleSide(state, 2, 0, 'Right');
    expect(state.lifts[2].sets[0]).toMatchObject({ leftDone: true, done: true });
  });
});

describe('sessionTimes', () => {
  it('keeps a normal session as it is', () => {
    expect(sessionTimes('2026-11-25T13:00:00.000Z', '2026-11-25T14:30:00.000Z')).toEqual({
      startedAt: '2026-11-25T13:00:00.000Z',
      endedAt: '2026-11-25T14:30:00.000Z',
    });
  });

  it('never ends before it starts', () => {
    expect(sessionTimes('2026-11-25T13:00:00.000Z', '2026-11-25T12:00:00.000Z').endedAt).toBe('2026-11-25T13:00:00.000Z');
  });

  it('gives a draft started over 12 hours ago a two-hour session ending at finish', () => {
    expect(sessionTimes('2026-11-24T18:00:00.000Z', '2026-11-25T14:52:00.000Z')).toEqual({
      startedAt: '2026-11-25T12:52:00.000Z',
      endedAt: '2026-11-25T14:52:00.000Z',
    });
  });
});

describe('startClockOnFirstTick', () => {
  const NOW = '2026-11-25T13:50:00.000Z';

  it('moves the start to the first tick of a draft opened earlier', () => {
    const opened = freshGymSession();
    expect(startClockOnFirstTick(opened, toggleSet(opened, 0, 0), NOW).startedAt).toBe(NOW);
  });

  it('leaves the start alone once something was ticked', () => {
    const mid = midGymSession();
    expect(startClockOnFirstTick(mid, toggleSet(mid, 0, 1), NOW).startedAt).toBe(GYM_STARTED_AT);
  });
});
