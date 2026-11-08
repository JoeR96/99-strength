/** Weight-trend fixtures: a cut from 95 kg with the brief's checkpoints. */

import { addDays, type LocalDate } from '@/lib/localDate';
import type { PaceDto, TrendPointDto, WeightTrendDto } from '@/types/bodyWeight';

/** `days` daily points ending on `to`, drifting from 95 kg at `kgPerDay`, with a wobble. */
export function trendPointsFixture(to: LocalDate, days = 30, kgPerDay = -0.08): TrendPointDto[] {
  const from = addDays(to, -(days - 1));
  const raw: { date: LocalDate; kg: number }[] = [];
  for (let i = 0; i < days; i++) {
    const wobble = ((i * 37) % 7) / 10 - 0.3; // deterministic ±0.3 kg
    raw.push({ date: addDays(from, i), kg: Math.round((95 + i * kgPerDay + wobble) * 10) / 10 });
  }
  let ewma = raw[0]?.kg ?? 0;
  return raw.map((p, i) => {
    ewma = i === 0 ? p.kg : Math.round((0.1 * p.kg + 0.9 * ewma) * 100) / 100;
    const window = raw.slice(Math.max(0, i - 6), i + 1);
    const movingAverage7 =
      window.length === 7 ? Math.round((window.reduce((s, w) => s + w.kg, 0) / 7) * 100) / 100 : null;
    return { date: p.date, kg: p.kg, movingAverage7, ewma };
  });
}

export function paceFixture(pace: PaceDto['pace'], trendKg: number): PaceDto {
  const targetKgToday = pace === 'OnPace' ? trendKg : pace === 'Behind' ? trendKg - 0.9 : pace === 'Ahead' ? trendKg + 0.9 : 0;
  return { pace, targetKgToday, trendKg, deltaKg: Math.round((trendKg - targetKgToday) * 10) / 10 };
}

export function weightTrendFixture(to: LocalDate, pace: PaceDto['pace'] = 'OnPace', days = 30): WeightTrendDto {
  const points = trendPointsFixture(to, days);
  const last = points[points.length - 1];
  return { points, pace: pace === 'NoTarget' ? null : paceFixture(pace, last?.ewma ?? 0) };
}
