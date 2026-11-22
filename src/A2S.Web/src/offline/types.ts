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
import type { FinishGymSessionRequest } from '@/types/gym';
import type { LogTrackSessionRequest } from '@/types/cardioTracks';

export type OfflineOpKind = 'habit.increment' | 'habit.set' | 'bodyweight.put' | 'plan.complete' | 'gym.finish' | 'cardio.log';

interface OpBase {
  /**
   * Stable id (nanoid) used to remove or update the op. It is also sent as the `Idempotency-Key` header,
   * on the first attempt and on every replay, so the server runs the write once however often it arrives.
   */
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

/**
 * A gym-mode finish. The payload carries the client session id generated when the session started,
 * so replaying it any number of times completes the session once (the server returns the first result).
 */
export interface GymFinishOp extends OpBase {
  kind: 'gym.finish';
  target: { clientSessionId: string };
  payload: FinishGymSessionRequest;
}

/**
 * A cardio track session from the log sheet. `payload.clientKey` is a UUID made when the sheet opened, so
 * the server records the session once (`client:{key}`) even without the Idempotency-Key header.
 */
export interface CardioLogOp extends OpBase {
  kind: 'cardio.log';
  target: { trackId: string };
  payload: LogTrackSessionRequest;
}

export type OfflineOp = HabitIncrementOp | HabitSetOp | BodyWeightPutOp | PlanCompleteOp | GymFinishOp | CardioLogOp;

type DistributiveOmit<T, K extends PropertyKey> = T extends unknown ? Omit<T, K> : never;

/**
 * What callers hand to the queue; createdAt and attempts are filled in, and so is id unless given (the
 * mutation passes the key it already sent on the first attempt, so a replay reuses it).
 */
export type NewOfflineOp = DistributiveOmit<OfflineOp, 'id' | 'createdAt' | 'attempts'> & { id?: string };
