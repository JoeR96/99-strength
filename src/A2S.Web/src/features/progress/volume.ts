/** Shapes the day budget into bars per muscle bucket against its cap. */

import type { DayBudgetDto } from '@/types/budget';

/** The brief's default weekly hard-set caps; the API only states a cap inside a warning. */
export const DEFAULT_WEEKLY_CAPS: Record<string, number> = {
  Chest: 10,
  Back: 14,
  Shoulders: 10,
  Quads: 10,
  Hamstrings: 10,
  Glutes: 12,
  Core: 12,
};

export interface VolumeBar {
  bucket: string;
  sets: number;
  cap: number | null;
  /** 0..1 share of the cap; 1 when at or over. */
  fill: number;
  over: boolean;
  message: string | null;
}

export function volumeBars(budget: DayBudgetDto | undefined, caps: Record<string, number> = DEFAULT_WEEKLY_CAPS): VolumeBar[] {
  if (!budget) return [];
  const warningsByBucket = new Map(budget.volumeWarnings.map((w) => [w.bucket, w]));
  const buckets = new Set<string>([...Object.keys(budget.hardSetsThisWeek), ...budget.volumeWarnings.map((w) => w.bucket)]);
  return [...buckets]
    .map((bucket) => {
      const sets = budget.hardSetsThisWeek[bucket] ?? warningsByBucket.get(bucket)?.sets ?? 0;
      const cap = warningsByBucket.get(bucket)?.cap ?? caps[bucket] ?? null;
      const over = cap !== null && sets > cap;
      const fill = cap === null || cap <= 0 ? 0 : Math.min(1, sets / cap);
      return { bucket, sets, cap, fill, over, message: warningsByBucket.get(bucket)?.message ?? null };
    })
    .sort((a, b) => b.sets - a.sets);
}
