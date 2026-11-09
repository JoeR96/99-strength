/**
 * Hevy's corner of Today, shown only when there is something to say: how many pulled workouts
 * are waiting in the inbox, that the last pull failed (with a retry), or that sync is waiting
 * for a connection. None of it blocks the page.
 */

import { Link } from 'react-router-dom';
import { ChevronRight, Inbox, RefreshCw, TriangleAlert, WifiOff } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface HevySyncNoticeProps {
  /** Unmatched workouts in the inbox. */
  inboxCount: number;
  /** Why the last pull failed, or null when it did not. */
  pullError?: string | null;
  onRetry?: () => void;
  retrying?: boolean;
  /** A key is stored but there is no connection, so nothing is being pulled. */
  offline?: boolean;
}

function inboxChipLabel(count: number): string {
  return `${count} unmatched Hevy ${count === 1 ? 'workout' : 'workouts'}`;
}

export function HevySyncNotice({ inboxCount, pullError = null, onRetry, retrying = false, offline = false }: HevySyncNoticeProps) {
  if (inboxCount <= 0 && !pullError && !offline) return null;

  return (
    <section className="space-y-2" aria-label="Hevy sync">
      {inboxCount > 0 && (
        <Link
          to="/hevy/inbox"
          className="flex min-h-11 items-center gap-2 rounded-lg border border-primary/40 bg-primary/10 px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-primary/15"
          data-testid="hevy-inbox-chip"
        >
          <Inbox className="h-4 w-4 shrink-0 text-primary" aria-hidden="true" />
          <span className="min-w-0 flex-1">{inboxChipLabel(inboxCount)}</span>
          <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
        </Link>
      )}

      {pullError && !offline && (
        <div
          className="flex items-center gap-3 rounded-lg border border-warning/50 bg-warning/10 px-3 py-2"
          role="status"
          data-testid="hevy-pull-failed"
        >
          <TriangleAlert className="h-4 w-4 shrink-0 text-warning" aria-hidden="true" />
          <p className="min-w-0 flex-1 text-sm text-foreground">{pullError}</p>
          {onRetry && (
            <Button variant="outline" className="h-11 shrink-0 px-3 text-sm" onClick={onRetry} disabled={retrying}>
              <RefreshCw className={retrying ? 'animate-spin' : undefined} aria-hidden="true" />
              Retry
            </Button>
          )}
        </div>
      )}

      {offline && (
        <p className="flex items-center gap-2 text-sm text-muted-foreground" role="status" data-testid="hevy-offline">
          <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
          Offline: Hevy will sync when you're back online.
        </p>
      )}
    </section>
  );
}
