/** What happens after an op replays: which queries to refetch, and how to describe a refused one. */

import type { QueryClient } from '@tanstack/react-query';
import { formatDayLabel } from '@/lib/localDate';
import { habitKeys } from '@/hooks/useHabits';
import { planKeys } from '@/hooks/usePlan';
import { bodyWeightKeys } from '@/hooks/useBodyWeight';
import type { OfflineOp } from './types';

export function invalidateFor(queryClient: QueryClient, op: OfflineOp): Promise<unknown> {
  switch (op.kind) {
    case 'habit.increment':
    case 'habit.set':
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: habitKeys.today(op.localDate) }),
        queryClient.invalidateQueries({ queryKey: [...habitKeys.all, 'history'] }),
      ]);
    case 'bodyweight.put':
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: bodyWeightKeys.all }),
        queryClient.invalidateQueries({ queryKey: habitKeys.today(op.localDate) }),
      ]);
    case 'plan.complete':
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'today'] }),
        queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'weekly-goals'] }),
        queryClient.invalidateQueries({ queryKey: [...planKeys.all, 'calendar'] }),
        queryClient.invalidateQueries({ queryKey: planKeys.active() }),
      ]);
  }
}

/** "Couldn't save Water for Mon 9 Nov; the server refused it." */
export function describeDrop(op: OfflineOp): string {
  const day = formatDayLabel(op.localDate);
  switch (op.kind) {
    case 'habit.increment':
    case 'habit.set':
      return `Couldn't save ${op.label ?? 'a habit'} for ${day}; the server refused it.`;
    case 'bodyweight.put':
      return `Couldn't save your ${op.payload.kg} kg weigh-in for ${day}; the server refused it.`;
    case 'plan.complete':
      return `Couldn't mark ${op.label ?? 'a session'} done for ${day}; the server refused it.`;
  }
}
