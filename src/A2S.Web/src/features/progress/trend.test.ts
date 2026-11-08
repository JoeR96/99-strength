import { describe, it, expect } from 'vitest';
import { buildTrendSeries, formatDelta, interpolateTarget, paceLabel, targetAnchors, yDomain } from './trend';
import { volumeBars } from './volume';
import { planFixture } from '@/mocks/fixtures/plan';
import { trendPointsFixture } from '@/mocks/fixtures/bodyWeight';
import { dayBudgetFixture } from '@/mocks/fixtures/budget';

describe('paceLabel', () => {
  it('maps each pace to text, tone and a signed delta', () => {
    expect(paceLabel({ pace: 'OnPace', targetKgToday: 93, trendKg: 93.1, deltaKg: 0.1 })).toEqual({ text: 'On pace', tone: 'success', delta: '+0.1 kg' });
    expect(paceLabel({ pace: 'Behind', targetKgToday: 93, trendKg: 93.9, deltaKg: 0.9 })).toEqual({ text: 'Behind', tone: 'warning', delta: '+0.9 kg' });
    expect(paceLabel({ pace: 'Ahead', targetKgToday: 93, trendKg: 92.4, deltaKg: -0.6 })).toEqual({ text: 'Ahead', tone: 'primary', delta: '−0.6 kg' });
    expect(paceLabel({ pace: 'NoTarget', targetKgToday: 0, trendKg: 93, deltaKg: 0 })).toEqual({ text: 'No target', tone: 'muted', delta: null });
    expect(paceLabel(null)).toEqual({ text: 'No target', tone: 'muted', delta: null });
  });

  it('formatDelta uses a real minus sign and one decimal', () => {
    expect(formatDelta(0)).toBe('0.0 kg');
    expect(formatDelta(0.04)).toBe('0.0 kg');
    expect(formatDelta(-1.26)).toBe('−1.3 kg');
  });
});

describe('interpolateTarget', () => {
  const anchors = targetAnchors(planFixture.startDate, planFixture.startWeightKg, planFixture.checkpoints);

  it('is the start weight on the start date and the checkpoint on its date', () => {
    expect(interpolateTarget(anchors, '2026-11-09')).toBe(95);
    expect(interpolateTarget(anchors, '2026-12-07')).toBe(92);
    expect(interpolateTarget(anchors, '2027-03-08')).toBe(85);
  });

  it('draws a straight line between anchors', () => {
    // 2026-11-09 (95) → 2026-12-07 (92) is 28 days; day 14 is halfway.
    expect(interpolateTarget(anchors, '2026-11-23')).toBe(93.5);
  });

  it('is null outside the anchor range and with no anchors', () => {
    expect(interpolateTarget(anchors, '2026-11-01')).toBeNull();
    expect(interpolateTarget(anchors, '2027-04-01')).toBeNull();
    expect(interpolateTarget([], '2026-11-09')).toBeNull();
  });
});

describe('buildTrendSeries', () => {
  it('returns an empty series for no points and no anchors', () => {
    expect(buildTrendSeries([], [])).toEqual([]);
    expect(yDomain([])).toEqual([80, 100]);
  });

  it('merges points with checkpoint dates and marks the checkpoint', () => {
    const points = trendPointsFixture('2026-12-10', 10);
    const anchors = targetAnchors(planFixture.startDate, planFixture.startWeightKg, planFixture.checkpoints);
    const rows = buildTrendSeries(points, anchors, planFixture.checkpoints);
    const checkpointRow = rows.find((r) => r.date === '2026-12-07');
    expect(checkpointRow?.checkpointKg).toBe(92);
    expect(checkpointRow?.target).toBe(92);
    expect(rows[0].date <= rows[rows.length - 1].date).toBe(true);
    expect(rows.filter((r) => r.kg !== null)).toHaveLength(10);
  });

  it('leaves the 7-day average null when fewer than seven points', () => {
    const points = trendPointsFixture('2026-11-12', 4);
    expect(points.every((p) => p.movingAverage7 === null)).toBe(true);
    const rows = buildTrendSeries(points, []);
    expect(rows.every((r) => r.movingAverage7 === null)).toBe(true);
  });
});

describe('volumeBars', () => {
  it('uses the warning cap when present, the default otherwise, and sorts by sets', () => {
    const bars = volumeBars(dayBudgetFixture());
    expect(bars[0]).toMatchObject({ bucket: 'Back', sets: 16, cap: 14, over: true, fill: 1 });
    expect(bars.find((b) => b.bucket === 'Chest')).toMatchObject({ sets: 9, cap: 10, over: false, fill: 0.9 });
  });

  it('is empty without a budget', () => {
    expect(volumeBars(undefined)).toEqual([]);
  });
});
