import { describe, expect, it } from 'vitest';
import { trackFinisherGymSession } from '../gymFixtures';
import { finisherLogFromSummary, finisherPhases } from './gymFinisherPlan';

describe('gym finisher plan', () => {
  it('turns the stair track finisher into warm-up, work and cool-down', () => {
    const finisher = trackFinisherGymSession().finisher!;

    expect(finisherPhases(finisher).map((p) => p.kind)).toEqual(['warmup', 'work', 'cooldown']);
  });

  it('gives no wizard to a protocol or fixed finisher', () => {
    expect(finisherPhases(null)).toEqual([]);
    expect(finisherPhases({ ...trackFinisherGymSession().finisher!, trackId: null })).toEqual([]);
  });

  it('prefills minutes, work minutes and level, leaving effort and floors', () => {
    const finisher = trackFinisherGymSession().finisher!;

    expect(finisherLogFromSummary(finisher, { totalSeconds: 13 * 60 + 10, workSeconds: 7 * 60, level: 6, rounds: 0 })).toEqual({ minutes: 13, workMinutes: 7, level: 6 });
  });
});
