/**
 * Body-weight types mirroring `A2S.Application.DTOs.BodyWeightDtos`.
 * `Pace` serialises as a **string**; dates are `yyyy-MM-dd` local dates.
 */

import type { LocalDate } from '@/lib/localDate';

export interface BodyWeightEntryDto {
  date: LocalDate;
  kg: number;
  recordedAtUtc: string;
}

export interface TrendPointDto {
  date: LocalDate;
  kg: number;
  /** Absent until there are seven days of data. */
  movingAverage7: number | null;
  ewma: number;
}

export type Pace = 'OnPace' | 'Behind' | 'Ahead' | 'NoTarget';

export interface PaceDto {
  pace: Pace;
  targetKgToday: number;
  trendKg: number;
  deltaKg: number;
}

export interface WeightTrendDto {
  points: TrendPointDto[];
  pace: PaceDto | null;
}

export interface RecordBodyWeightRequest {
  kg: number;
}
