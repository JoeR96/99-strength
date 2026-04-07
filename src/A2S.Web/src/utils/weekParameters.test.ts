import { describe, it, expect, vi } from 'vitest';
import {
  getWeekParameters,
  getTemplateWeek,
  getBlockType,
  roundToGymIncrement,
} from './weekParameters';

describe('getWeekParameters', () => {
  it('returns correct params for week 1', () => {
    const params = getWeekParameters(1);
    expect(params.intensity).toBe(0.65);
    expect(params.sets).toBe(4);
    expect(params.targetReps).toBe(12);
    expect(params.repOutTarget).toBe(15);
    expect(params.isDeload).toBe(false);
  });

  it('returns correct params for week 6', () => {
    const params = getWeekParameters(6);
    expect(params.intensity).toBe(0.73);
    expect(params.sets).toBe(4);
    expect(params.targetReps).toBe(9);
    expect(params.repOutTarget).toBe(11);
    expect(params.isDeload).toBe(false);
  });

  it('returns deload params for week 7', () => {
    const params = getWeekParameters(7);
    expect(params.intensity).toBe(0.60);
    expect(params.sets).toBe(4);
    expect(params.targetReps).toBe(5);
    expect(params.repOutTarget).toBeNull();
    expect(params.isDeload).toBe(true);
  });

  it('returns deload params for week 14', () => {
    const params = getWeekParameters(14);
    expect(params.isDeload).toBe(true);
    expect(params.repOutTarget).toBeNull();
  });

  it('returns deload params for week 21', () => {
    const params = getWeekParameters(21);
    expect(params.isDeload).toBe(true);
    expect(params.intensity).toBe(0.60);
  });

  it('returns correct params for week 20 (highest intensity non-deload)', () => {
    const params = getWeekParameters(20);
    expect(params.intensity).toBe(0.79);
    expect(params.targetReps).toBe(7);
    expect(params.repOutTarget).toBe(9);
    expect(params.isDeload).toBe(false);
  });

  it('returns week 1 defaults for out-of-range week 0', () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const params = getWeekParameters(0);
    expect(params.intensity).toBe(0.65);
    expect(params.sets).toBe(4);
    spy.mockRestore();
  });

  it('returns week 1 defaults for out-of-range week 22', () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const params = getWeekParameters(22);
    expect(params.intensity).toBe(0.65);
    spy.mockRestore();
  });

  it('intensity increases across blocks', () => {
    const week1 = getWeekParameters(1);
    const week8 = getWeekParameters(8);
    const week15 = getWeekParameters(15);
    expect(week8.intensity).toBeGreaterThan(week1.intensity);
    expect(week15.intensity).toBeGreaterThan(week8.intensity);
  });
});

describe('getTemplateWeek', () => {
  it('maps program week 1 with default sequence to template week 1', () => {
    expect(getTemplateWeek(1, [1, 1, 2, 3])).toBe(1);
  });

  it('maps program week 7 to template week 7', () => {
    expect(getTemplateWeek(7, [1, 1, 2, 3])).toBe(7);
  });

  it('maps program week 8 (block 2 = type 1) to template week 1', () => {
    expect(getTemplateWeek(8, [1, 1, 2, 3])).toBe(1);
  });

  it('maps program week 15 (block 3 = type 2) to template week 8', () => {
    expect(getTemplateWeek(15, [1, 1, 2, 3])).toBe(8);
  });

  it('maps program week 22 (block 4 = type 3) to template week 15', () => {
    expect(getTemplateWeek(22, [1, 1, 2, 3])).toBe(15);
  });

  it('defaults to block type 1 when sequence index is out of range', () => {
    expect(getTemplateWeek(50, [1])).toBe(1);
  });
});

describe('getBlockType', () => {
  it('returns block type for first block', () => {
    expect(getBlockType(1, [1, 2, 3])).toBe(1);
  });

  it('returns block type for second block', () => {
    expect(getBlockType(8, [1, 2, 3])).toBe(2);
  });

  it('defaults to 1 for out-of-range', () => {
    expect(getBlockType(100, [1, 2])).toBe(1);
  });
});

describe('roundToGymIncrement', () => {
  it('rounds to nearest 2.5 kg', () => {
    expect(roundToGymIncrement(61.3, 'kg')).toBe(62.5);
    expect(roundToGymIncrement(63.8, 'kg')).toBe(65);
    expect(roundToGymIncrement(100, 'kg')).toBe(100);
  });

  it('rounds to nearest 5 lbs', () => {
    expect(roundToGymIncrement(132, 'lbs')).toBe(130);
    expect(roundToGymIncrement(137, 'lbs')).toBe(135);
    expect(roundToGymIncrement(225, 'lbs')).toBe(225);
  });

  it('defaults to kg when no unit specified', () => {
    expect(roundToGymIncrement(61.3)).toBe(62.5);
  });
});
