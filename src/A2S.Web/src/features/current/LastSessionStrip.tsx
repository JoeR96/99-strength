/**
 * The session just finished (today's completed lift), kept small above the next one: "In Hevy ✓"
 * once the server's post landed, or "Not in Hevy yet" with Retry while it is pending or failed.
 */

import { Check, CloudOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { SessionKind, SessionStatus, type TodaySessionDto } from '@/types/plan';

export interface LastSessionStripProps {
  sessions: TodaySessionDto[];
  onRetry: (gymSessionId: string) => void;
  retrying: boolean;
  hevyConfigured: boolean;
}

export function LastSessionStrip({ sessions, onRetry, retrying, hevyConfigured }: LastSessionStripProps) {
  const done = sessions.filter((s) => s.kind === SessionKind.Lift && s.status === SessionStatus.Done);
  const last = done[done.length - 1];
  if (!last) return null;
  const notInHevy = last.hevyPostStatus === 'Pending' || last.hevyPostStatus === 'Failed';
  // UpdatePending: already in Hevy, a later change still to send — shown as Posted.
  // Posted by the server, or logged in Hevy itself and pulled (a Hevy-sourced completion has no post).
  const inHevy = hevyConfigured && (last.hevyPostStatus === 'Posted' || last.hevyPostStatus === 'UpdatePending' || last.hevyPostStatus == null);

  return (
    <section
      className={`flex flex-wrap items-center gap-2 rounded-lg border p-3 ${notInHevy ? 'border-warning/50 bg-warning/10' : 'border-border bg-card'}`}
      aria-label="Last session"
      data-testid="last-session"
    >
      {notInHevy ? <CloudOff className="h-5 w-5 shrink-0 text-warning" aria-hidden="true" /> : <Check className="h-5 w-5 shrink-0 text-primary" aria-hidden="true" />}
      <p className="min-w-0 flex-1 text-sm text-foreground">
        <span className="font-medium">{last.label} done.</span>{' '}
        {notInHevy && <span data-testid="not-in-hevy">Not in Hevy yet{last.hevyPostStatus === 'Failed' ? ': Hevy refused it' : ''}</span>}
        {inHevy && <span data-testid="in-hevy">In Hevy ✓</span>}
      </p>
      {notInHevy && last.gymSessionId && (
        <Button
          variant="outline"
          className="h-11"
          onClick={() => onRetry(last.gymSessionId!)}
          disabled={retrying}
          data-testid="retry-hevy"
          aria-label={`Retry sending ${last.label} to Hevy`}
        >
          Retry
        </Button>
      )}
    </section>
  );
}
