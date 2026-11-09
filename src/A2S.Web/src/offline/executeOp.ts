/** Turns a queued op back into the API call it stands for. Nothing is recomputed: same route, same body. */

import { habitsApi } from '@/api/habits';
import { plansApi } from '@/api/plans';
import { bodyWeightApi } from '@/api/bodyWeight';
import type { OfflineOp } from './types';

export function executeOp(op: OfflineOp): Promise<unknown> {
  switch (op.kind) {
    case 'habit.increment':
      return habitsApi.increment(op.target.habitId, op.localDate, op.payload);
    case 'habit.set':
      return habitsApi.setLog(op.target.habitId, op.localDate, op.payload);
    case 'bodyweight.put':
      return bodyWeightApi.record(op.localDate, op.payload.kg);
    case 'plan.complete':
      return plansApi.completeSession(op.target.planId, op.payload);
  }
}
