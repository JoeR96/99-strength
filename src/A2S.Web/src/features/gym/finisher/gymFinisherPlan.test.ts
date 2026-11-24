import { describe, expect, it } from 'vitest';
import { cardioGymSession, stairStage1Block, stairStage2PendingBlock, trackFinisherGymSession } from '../gymFixtures';
import { cardioLogFromSummary, cardioPhases, finisherLogFromSummary, finisherPhases, wizardTarget } from './gymFinisherPlan';
import { setHardCardioSkipped } from '../gymCardio';

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

describe('stair climber cardio exercise in the wizard', () => {
  it('builds warm-up → work → cool-down from a continuous block', () => {
    expect(cardioPhases(stairStage1Block).map((p) => [p.kind, p.seconds, p.level])).toEqual([
      ['warmup', 180, 3],
      ['work', 360, 6],
      ['cooldown', 120, 3],
    ]);
  });

  it('builds 4 × 4/3 with recoveries only between rounds: 30 min with warm-up and cool-down', () => {
    const phases = cardioPhases({ ...stairStage2PendingBlock, intensity: 8, isSettingsPending: false });

    expect(phases.filter((p) => p.kind === 'hard').map((p) => p.level)).toEqual([8, 8, 8, 8]);
    expect(phases.filter((p) => p.kind === 'easy')).toHaveLength(3);
    expect(phases.reduce((total, p) => total + p.seconds, 0)).toBe(30 * 60);
  });

  it('prefills total seconds, work minutes and level from the summary', () => {
    const item = cardioGymSession().cardio[0];

    expect(cardioLogFromSummary(item, { totalSeconds: 11 * 60 + 5, workSeconds: 6 * 60, level: 7, rounds: 0 })).toEqual({ totalSeconds: 665, workMinutes: 6, intensity: 7 });
  });

  it('runs the wizard for the stair finisher first, else the stair climber, never a skipped or pending one', () => {
    expect(wizardTarget(trackFinisherGymSession())).toMatchObject({ name: 'Stair finisher', cardioIndex: null });
    expect(wizardTarget(cardioGymSession())).toMatchObject({ name: 'Stair Machine (Floors)', cardioIndex: 0 });
    expect(wizardTarget(setHardCardioSkipped(cardioGymSession(), true))).toBeNull();
    expect(wizardTarget(cardioGymSession(stairStage2PendingBlock))).toBeNull();
  });
});
