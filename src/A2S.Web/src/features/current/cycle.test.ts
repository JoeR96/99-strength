import { describe, it, expect } from 'vitest';
import { estimateMinutes, needsTrainingMax, previewHref, setSummary, splitCycle } from './cycle';
import { cycleFixture, sessionPlan } from '@/mocks/fixtures/cycle';
import { stairExercise, stairStage2PendingBlock, walkExercise, walkPendingBlock } from '@/features/gym/gymFixtures';

describe('cycle helpers', () => {
  it('summarises A2S2 sets with the AMRAP and the weight', () => {
    expect(setSummary(sessionPlan(1, 1).exercises[0])).toBe('4 × 5, 1 × 10+ · 75 kg');
    expect(setSummary(sessionPlan(7, 1, { deload: true }).exercises[0])).toBe('4 × 5 · 75 kg');
    expect(setSummary(sessionPlan(1, 1, { pendingTm: true }).exercises[0])).toBe('4 × 5, 1 × 10+ · set TM');
  });

  it('summarises reps-per-set work as its rep range', () => {
    expect(setSummary(sessionPlan(1, 1).exercises[1])).toBe('3 × 8–12 · 50 kg');
  });

  it('summarises a cardio exercise as its block text, or what to set first', () => {
    expect(setSummary(stairExercise())).toBe('3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)');
    expect(setSummary(stairExercise(stairStage2PendingBlock))).toBe('Set the hard level first');
    expect(setSummary(walkExercise(walkPendingBlock))).toBe('Set speed and incline first');
  });

  it("counts a cardio exercise's block minutes in the estimate", () => {
    const plan = sessionPlan(1, 1);
    const withStairs = { ...plan, exercises: [...plan.exercises, stairExercise()] };
    expect(estimateMinutes(withStairs, 10)).toBe(estimateMinutes(plan, 10) + 11);
  });

  it('estimates the session from sets, rest, the finisher and the warm-up', () => {
    // Bench 5 × (180 + 40) s + row 3 × (90 + 40) s = 1490 s ≈ 24.8 min, + 11 min stairs (3 easy, 5 hard, 3 easy) + 10 min warm-up.
    expect(estimateMinutes(sessionPlan(1, 1), 10)).toBe(46);
  });

  it('splits the cycle into current, the rest of this week and next week', () => {
    const split = splitCycle(cycleFixture({ week: 2, day: 3 }));
    expect([split.current!.week, split.current!.day]).toEqual([2, 3]);
    expect(split.thisWeek.map((s) => s.day)).toEqual([4]);
    expect(split.nextWeek.map((s) => `${s.week}-${s.day}`)).toEqual(['3-1', '3-2', '3-3', '3-4']);
  });

  it('previews a cycle position by week and day', () => {
    expect(previewHref({ week: 4, day: 2 })).toBe('/gym/2?preview=1&week=4');
    expect(needsTrainingMax(sessionPlan(1, 1, { pendingTm: true }))).toBe(true);
    expect(needsTrainingMax(sessionPlan(1, 2, { pendingTm: true }))).toBe(false);
  });
});
