/**
 * One of today's sessions. Lifts get a big Start that opens the session page;
 * cardio and mobility get Mark done, which the page turns into a log sheet.
 * A paused day shows the status and nothing to press.
 */

import { Link } from 'react-router-dom';
import { CheckCircle2, Dumbbell, ExternalLink, HeartPulse, Moon, Play, StretchHorizontal, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { SessionKind, SessionStatus, type TodaySessionDto } from '@/types/plan';

export interface SessionCardProps {
  session: TodaySessionDto;
  /** True when the day falls in a Pause phase: no Start, no Mark done. */
  paused?: boolean;
  onMarkDone?: (session: TodaySessionDto) => void;
  onUndo?: (session: TodaySessionDto) => void;
  /** A Hevy routine deep link; Phase 6 supplies it, so until then it is absent and the button hidden. */
  hevyRoutineUrl?: string | null;
  busy?: boolean;
}

const kindIcon = {
  [SessionKind.Rest]: Moon,
  [SessionKind.Lift]: Dumbbell,
  [SessionKind.Cardio]: HeartPulse,
  [SessionKind.Mobility]: StretchHorizontal,
} as const;

const kindName = {
  [SessionKind.Rest]: 'Rest day',
  [SessionKind.Lift]: 'Lift',
  [SessionKind.Cardio]: 'Cardio',
  [SessionKind.Mobility]: 'Mobility',
} as const;

function statusBadge(status: SessionStatus) {
  switch (status) {
    case SessionStatus.Done:
      return <Badge variant="success">Done</Badge>;
    case SessionStatus.Missed:
      return <Badge variant="warning">Missed</Badge>;
    case SessionStatus.Paused:
      return <Badge variant="warning">Paused</Badge>;
    case SessionStatus.Rest:
      return <Badge variant="neutral">Rest</Badge>;
    default:
      return <Badge variant="primary">Planned</Badge>;
  }
}

export function SessionCard({ session, paused = false, onMarkDone, onUndo, hevyRoutineUrl, busy = false }: SessionCardProps) {
  const Icon = kindIcon[session.kind];
  const isRest = session.kind === SessionKind.Rest;
  const isDone = session.status === SessionStatus.Done;
  const inactive = paused || session.status === SessionStatus.Paused;
  const canAct = !isRest && !isDone && !inactive;

  return (
    <article
      className={cn('rounded-lg border bg-card p-4', isDone ? 'border-success/60' : 'border-border')}
      aria-label={`${kindName[session.kind]}: ${session.label}`}
      data-testid={`session-card-${session.kind}`}
    >
      <div className="flex items-start gap-3">
        <div
          className={cn(
            'flex h-12 w-12 shrink-0 items-center justify-center rounded-lg',
            isDone ? 'bg-success/15 text-success' : 'bg-primary/10 text-primary'
          )}
        >
          {isDone ? <CheckCircle2 className="h-6 w-6" aria-hidden="true" /> : <Icon className="h-6 w-6" aria-hidden="true" />}
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-xs font-medium text-muted-foreground">{kindName[session.kind]}</div>
          <h2 className="truncate text-xl font-semibold text-foreground">{session.label}</h2>
        </div>
        {statusBadge(session.status)}
      </div>

      {canAct && session.kind === SessionKind.Lift && session.liftDay && (
        <div className="mt-4 flex flex-col gap-2">
          <Button asChild size="lg" className="w-full" data-testid="start-session">
            <Link to={`/workout/session/${session.liftDay}`}>
              <Play aria-hidden="true" />
              Start
            </Link>
          </Button>
          {hevyRoutineUrl && (
            <Button asChild variant="outline" className="w-full">
              <a href={hevyRoutineUrl} target="_blank" rel="noreferrer">
                <ExternalLink aria-hidden="true" />
                Open in Hevy
              </a>
            </Button>
          )}
        </div>
      )}

      {canAct && (session.kind === SessionKind.Cardio || session.kind === SessionKind.Mobility) && (
        <Button size="lg" className="mt-4 w-full" onClick={() => onMarkDone?.(session)} disabled={busy} data-testid="mark-done">
          <CheckCircle2 aria-hidden="true" />
          Mark done
        </Button>
      )}

      {isDone && onUndo && session.completionId && (
        <Button variant="ghost" className="mt-3 h-11 w-full" onClick={() => onUndo(session)} disabled={busy}>
          <Undo2 aria-hidden="true" />
          Undo
        </Button>
      )}

      {inactive && !isRest && (
        <p className="mt-3 text-sm text-muted-foreground">Plan paused: this session is not counted.</p>
      )}
    </article>
  );
}
