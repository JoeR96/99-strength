/**
 * Gym mode types mirroring `A2S.Application.Commands.GymSessions` and the cardio protocol DTOs.
 * `day` is the A2S2 day number (integer enum); `effort` serialises as a string.
 */

import type { CompleteDayResult, ExercisePerformanceRequest } from '@/types/workoutDtos';
import type { HevyPostStatus } from '@/types/plan';

export type Effort = 'Easy' | 'Solid' | 'Brutal';

/** Easy = RPE 4, Solid = RPE 7, Brutal = RPE 9 (the server does the same mapping). */
export const effortRpe: Record<Effort, number> = { Easy: 4, Solid: 7, Brutal: 9 };

export interface FinisherLogRequest {
  durationSeconds: number;
  effort: Effort;
  level?: number | null;
  floors?: number | null;
  pain?: 'None' | 'Niggle' | 'Stop';
  /** A fixed cool-down ticked as done (treadmill 10 min): logged as prescribed. */
  completedAsPrescribed?: boolean;
}

/** Body for `POST /gym-sessions`. `clientSessionId` is generated when the session starts and makes the finish idempotent. */
export interface FinishGymSessionRequest {
  clientSessionId: string;
  workoutId: string;
  day: number;
  startedAt: string;
  endedAt: string;
  performances: ExercisePerformanceRequest[];
  warmUpTicks?: boolean[] | null;
  finisher?: FinisherLogRequest | null;
}

export interface FinishGymSessionResult {
  clientSessionId: string;
  completeDay: CompleteDayResult | null;
  completionId: string | null;
  finisherCompletionId: string | null;
  hevyStatus: HevyPostStatus;
  hevyWorkoutId: string | null;
  replayed: boolean;
}

export interface HevyPostStatusDto {
  clientSessionId: string;
  status: HevyPostStatus;
  hevyWorkoutId: string | null;
  attempts: number;
  lastError: string | null;
}

export const CardioProtocolKind = { NorwegianFourByFour: 0, CouchToFiveK: 1, Liss: 2 } as const;
export type CardioProtocolKind = (typeof CardioProtocolKind)[keyof typeof CardioProtocolKind];

export interface CardioIntervalDto {
  label: string;
  seconds: number;
  effort: number;
}

export interface CardioProtocolDto {
  id: string;
  name: string;
  kind: CardioProtocolKind;
  intervals: CardioIntervalDto[];
  totalMinutes: number;
  notes: string;
  weekIndex: number | null;
  repeatWeek: boolean;
}
