import { describe, expect, it } from 'vitest';
import { buildFinisherPlan, durationText, nextText, phaseCue } from './finisherPlan';

const warmUp = { minutes: 3, level: 3, zone: 'Z2' };
const coolDown = { minutes: 3, level: 3, zone: 'Z2' };

describe('buildFinisherPlan', () => {
  it('builds warm-up → continuous work → cool-down', () => {
    const phases = buildFinisherPlan({ warmUp, coolDown, workMinutes: 7, level: 6 });

    expect(phases).toEqual([
      { kind: 'warmup', seconds: 180, level: 3, zone: 'Z2', label: 'Warm-up' },
      { kind: 'work', seconds: 420, level: 6, zone: null, label: 'Work' },
      { kind: 'cooldown', seconds: 180, level: 3, zone: 'Z2', label: 'Cool-down' },
    ]);
  });

  it('expands 1:1 intervals into hard and easy rounds, easy at the warm-up level', () => {
    const phases = buildFinisherPlan({ warmUp, coolDown, workMinutes: 14, level: 6, intervals: { rounds: 7, workSeconds: 60, restSeconds: 60 } });

    expect(phases).toHaveLength(1 + 14 + 1);
    expect(phases[1]).toEqual({ kind: 'hard', seconds: 60, level: 6, zone: null, label: 'Hard', round: 1, rounds: 7 });
    expect(phases[2]).toMatchObject({ kind: 'easy', seconds: 60, level: 3, zone: 'Z2', round: 1 });
    expect(phases[14]).toMatchObject({ kind: 'easy', round: 7 });
  });

  it('skips the last easy phase when restAfterLastRound is false', () => {
    const phases = buildFinisherPlan({
      warmUp: { minutes: 3, level: 3, zone: null },
      coolDown: { minutes: 2, level: 3, zone: null },
      workMinutes: null,
      level: 8,
      intervals: { rounds: 4, workSeconds: 240, restSeconds: 180, restAfterLastRound: false },
    });

    expect(phases.map((p) => p.kind)).toEqual(['warmup', 'hard', 'easy', 'hard', 'easy', 'hard', 'easy', 'hard', 'cooldown']);
    expect(phases.filter((p) => p.kind === 'easy')).toHaveLength(3);
    expect(phases.reduce((total, p) => total + p.seconds, 0)).toBe(30 * 60);
  });

  it('uses the interval recovery level for easy rounds when given', () => {
    const phases = buildFinisherPlan({ warmUp, coolDown, workMinutes: null, level: 8, intervals: { rounds: 2, workSeconds: 60, restSeconds: 60, easyLevel: 4 } });

    expect(phases.filter((p) => p.kind === 'easy').map((p) => p.level)).toEqual([4, 4]);
  });

  it('skips segments that are off', () => {
    expect(buildFinisherPlan({ warmUp: { minutes: 0, level: null, zone: null }, coolDown: null, workMinutes: 5, level: 6 }).map((p) => p.kind)).toEqual(['work']);
  });
});

describe('phase text', () => {
  it('reads as cues', () => {
    const hard = { kind: 'hard' as const, seconds: 60, level: 6, zone: null, label: 'Hard' };
    expect(phaseCue(hard)).toBe('Hard: level 6 for 1 min');
    expect(nextText(hard)).toBe('1 min hard @ L6');
    expect(durationText(90)).toBe('1 min 30 s');
    expect(durationText(30)).toBe('30 s');
  });
});
