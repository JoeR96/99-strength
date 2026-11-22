/**
 * Turns a queued op back into the API call it stands for. Nothing is recomputed: same route, same body,
 * and the op id as the Idempotency-Key, so a replay the server already answered is not run twice.
 */

import { habitsApi } from '@/api/habits';
import { plansApi } from '@/api/plans';
import { bodyWeightApi } from '@/api/bodyWeight';
import { gymSessionsApi } from '@/api/gymSessions';
import { cardioTracksApi } from '@/api/cardioTracks';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import type { OfflineOp } from './types';

export function executeOp(op: OfflineOp): Promise<unknown> {
  switch (op.kind) {
    case 'habit.increment':
      return habitsApi.increment(op.target.habitId, op.localDate, op.payload, op.id);
    case 'habit.set':
      return habitsApi.setLog(op.target.habitId, op.localDate, op.payload, op.id);
    case 'bodyweight.put':
      return bodyWeightApi.record(op.localDate, op.payload.kg, op.id);
    case 'plan.complete':
      return plansApi.completeSession(op.target.planId, op.payload, op.id);
    case 'gym.finish':
      return gymSessionsApi.finish(op.payload, op.id);
    case 'cardio.log':
      return cardioTracksApi.logSession(op.target.trackId, op.payload, op.id);
    case 'mobility.feedback':
      return mobilityRoutinesApi.recordFeedback(op.target.routineId, op.payload, op.id);
  }
}
