/**
 * The offline queue's records. Each op is one API write that could not be sent
 * (no signal, or the request died on the wire). `payload` is exactly the request
 * body the API expects and `target` the route parameters, so replay sends the very
 * same request later. `localDate` and `clientTimestamp` are fixed when the person
 * tapped, never at replay: two water taps at 23:58 land on yesterday even when the
 * connection returns at 00:05.
 */

import type { LocalDate } from '@/lib/localDate';
import type { IncrementHabitRequest, SetHabitLogRequest } from '@/types/habits';
import type { CompleteSessionRequest } from '@/types/plan';
import type { RecordBodyWeightRequest } from '@/types/bodyWeight';

export type OfflineOpKind = 'habit.increment' | 'habit.set' | 'bodyweight.put' | 'plan.complete';

interface OpBase {
  /** Stable id (nanoid) used to remove or update the op. */
  id: string;
  /** Europe/London date the write belongs to, resolved when it was made. */
  localDate: LocalDate;
  /** ISO instant the op was queued. */
  createdAt: string;
  /** Failed replay attempts so far; drives the backoff. */
  attempts: number;
  /** Human name for toasts ("Water", "Norwegian 4×4"); optional. */
  label?: string;
}

export interface HabitIncrementOp extends OpBase {
  kind: 'habit.increment';
  target: { habitId: string };
  payload: IncrementHabitRequest;
}

export interface HabitSetOp extends OpBase {
  kind: 'habit.set';
  target: { habitId: string };
  payload: SetHabitLogRequest;
}

export interface BodyWeightPutOp extends OpBase {
  kind: 'bodyweight.put';
  target: Record<string, never>;
  payload: RecordBodyWeightRequest;
}

export interface PlanCompleteOp extends OpBase {
  kind: 'plan.complete';
  target: { planId: string };
  payload: CompleteSessionRequest;
}

export type OfflineOp = HabitIncrementOp | HabitSetOp | BodyWeightPutOp | PlanCompleteOp;

type DistributiveOmit<T, K extends PropertyKey> = T extends unknown ? Omit<T, K> : never;

/** What callers hand to the queue; id, createdAt and attempts are filled in. */
export type NewOfflineOp = DistributiveOmit<OfflineOp, 'id' | 'createdAt' | 'attempts'>;
