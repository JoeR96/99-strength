/**
 * Habit types mirroring `A2S.Application.DTOs.HabitDtos` and `HabitRequests`.
 * `HabitType` serialises as a **string** (`JsonStringEnumConverter`); every other
 * value is plain. Dates are `yyyy-MM-dd` Europe/London local dates.
 */

import type { LocalDate } from '@/lib/localDate';

export type HabitType = 'Counter' | 'Numeric' | 'Checkbox';

export interface HabitDto {
  id: string;
  name: string;
  type: HabitType;
  target: number;
  minimum: number;
  unit: string | null;
  order: number;
  archived: boolean;
  icon: string | null;
  externalKey: string | null;
}

export interface HabitTodayDto {
  id: string;
  name: string;
  type: HabitType;
  target: number;
  minimum: number;
  unit: string | null;
  value: number;
  metMinimum: boolean;
  metTarget: boolean;
  order: number;
  icon: string | null;
}

export interface HabitStreakDto {
  current: number;
  best: number;
  lastCountedDay: LocalDate | null;
  todayCounted: boolean;
  graceUsed: boolean;
}

export interface HabitsTodayDto {
  date: LocalDate;
  habits: HabitTodayDto[];
  streak: HabitStreakDto;
}

export interface HabitLogDto {
  habitId: string;
  date: LocalDate;
  value: number;
  updatedAtUtc: string;
}

export interface HabitHistoryEntryDto {
  habitId: string;
  date: LocalDate;
  value: number;
  metMinimum: boolean;
  metTarget: boolean;
}

export interface HabitHistoryDto {
  from: LocalDate;
  to: LocalDate;
  habits: HabitDto[];
  entries: HabitHistoryEntryDto[];
}

export interface CreateHabitRequest {
  name: string;
  type: HabitType;
  target: number;
  minimum: number;
  unit: string | null;
  order: number;
  icon: string | null;
}

/** The type cannot change after creation. */
export interface UpdateHabitRequest {
  name: string;
  target: number;
  minimum: number;
  unit: string | null;
  order: number;
  icon: string | null;
}

/** `clientTimestamp` is an ISO instant taken at tap time; the server keeps the newest. */
export interface SetHabitLogRequest {
  value: number;
  clientTimestamp: string;
}

export interface IncrementHabitRequest {
  delta: 1 | -1;
  clientTimestamp: string;
}
