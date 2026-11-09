/**
 * Hevy push/pull types mirroring `A2S.Application.Hevy` (PullSummary, HevyInboxItemDto,
 * AssignHevyWorkoutResult, PushRoutineResult, PushedSession, PushWeekResult) and the `HevyInboxController` /
 * `PlansController` request bodies. Plan enums serialise as integers; dates are `yyyy-MM-dd`.
 */

import type { LocalDate } from '@/lib/localDate';
import type { PlanDayNumber, SessionKind } from '@/types/plan';

/** `POST /hevy/pull`. `skippedReason` is set when nothing was attempted (no key, no active plan). */
export interface PullSummary {
  pulled: number;
  matched: number;
  unmatched: number;
  skipped: number;
  skippedReason: string | null;
  wasSkipped: boolean;
}

export interface HevyInboxExerciseDto {
  title: string;
  templateId: string;
  sets: number;
}

/** A pulled Hevy workout the plan could not place. */
export interface HevyInboxItemDto {
  hevyWorkoutId: string;
  title: string;
  startUtc: string;
  endUtc: string;
  localDate: LocalDate;
  routineId: string | null;
  reason: string | null;
  exercises: HevyInboxExerciseDto[];
}

/** Body for `POST /hevy/inbox/{id}/assign`. `extra` records the plan completion only (no A2S2 progression). */
export interface AssignHevyWorkoutRequest {
  date: LocalDate;
  kind: SessionKind;
  liftDay?: PlanDayNumber | null;
  extra?: boolean;
}

export interface AssignHevyWorkoutResult {
  hevyWorkoutId: string;
  completionId: string;
  date: LocalDate;
  kind: SessionKind;
  liftDay: PlanDayNumber | null;
}

/** Body for `POST /plans/{id}/sessions/push`. */
export interface PushSessionRequest {
  date: LocalDate;
  kind: SessionKind;
  liftDay?: PlanDayNumber | null;
}

export type PushOutcome = 'created' | 'updated';

export interface PushRoutineResult {
  routineId: string;
  title: string;
  outcome: PushOutcome;
}

export interface PushRoutineFailure {
  date: LocalDate;
  label: string;
  error: string;
}

/** One session the week push sent: its day and label, then what Hevy now holds. */
export interface PushedSession extends PushRoutineResult {
  date: LocalDate;
  label: string;
}

export interface PushWeekResult {
  weekStart: LocalDate;
  pushed: PushedSession[];
  failed: PushRoutineFailure[];
}
