/** Slot for PR 7's offline queue: how many taps are waiting to reach the server. */

import { CloudOff } from 'lucide-react';

export function PendingSyncBadge({ count }: { count: number }) {
  if (count <= 0) return null;
  return (
    <span
      className="inline-flex h-9 items-center gap-1.5 rounded-full border border-warning/50 bg-warning/10 px-3 text-sm font-medium text-warning"
      role="status"
      aria-label={`${count} ${count === 1 ? 'change' : 'changes'} waiting to sync`}
      data-testid="pending-sync"
    >
      <CloudOff className="h-4 w-4" aria-hidden="true" />
      {count} pending
    </span>
  );
}
