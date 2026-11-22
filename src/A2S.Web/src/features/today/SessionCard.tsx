/**
 * One of today's sessions. Lifts get a big Start that opens gym mode (`/gym/:day`);
 * cardio and mobility get Mark done, which the page turns into a log sheet.
 * Beside it sits Hevy: Open in Hevy once the session has a pushed routine, or
 * Push to Hevy when a key is stored and it has none. A paused day shows the
 * status and nothing to press. A lift finished in gym mode whose workout has not reached
 * Hevy yet says "Not in Hevy yet" with a Retry. Every button is at least 44 px tall.
 */

import { Link } from 'react-router-dom';
import { CheckCircle2, CloudOff, Dumbbell, ExternalLink, HeartPulse, Moon, Play, RefreshCw, StretchHorizontal, Undo2, Upload } from 'lucide-react';
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
  /** A Hevy key is stored, so a session without a routine can be pushed. */
  hevyConfigured?: boolean;
  /** Opens `session.hevyRoutineId` in Hevy (deep link with a web fallback). */
  onOpenInHevy?: (session: TodaySessionDto) => void;
  /** Pushes the session as a Hevy routine. */
  onPushToHevy?: (session: TodaySessionDto) => void;
  /** A push for this card is in flight. */
  pushing?: boolean;
  /** Retries the Hevy post of a lift finished in gym mode ("Not in Hevy yet"). */
  onRetryHevy?: (session: TodaySessionDto) => void;
  /** A Hevy retry for this card is in flight. */
  retrying?: boolean;
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

/** Side by side from 360 px; on anything narrower the two buttons stack so neither label is cut. */
const TWO_UP = 'grid-cols-1 min-[360px]:grid-cols-2';

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

export function SessionCard({
  session,
  paused = false,
  onMarkDone,
  onUndo,
  hevyConfigured = false,
  onOpenInHevy,
  onPushToHevy,
  pushing = false,
  onRetryHevy,
  retrying = false,
  busy = false,
}: SessionCardProps) {
  const Icon = kindIcon[session.kind];
  const isRest = session.kind === SessionKind.Rest;
  const isDone = session.status === SessionStatus.Done;
  const inactive = paused || session.status === SessionStatus.Paused;
  const canAct = !isRest && !isDone && !inactive;
  const isPlanned = session.status === SessionStatus.Planned;

  const showOpenInHevy = canAct && isPlanned && !!session.hevyRoutineId;
  const showPushToHevy = canAct && isPlanned && !session.hevyRoutineId && hevyConfigured;
  const hasHevyButton = showOpenInHevy || showPushToHevy;

  const hevyButton = showOpenInHevy ? (
    <Button
      variant="outline"
      size="lg"
      className="w-full px-3 text-base"
      onClick={() => onOpenInHevy?.(session)}
      data-testid="open-in-hevy"
      aria-label={`Open in Hevy: ${session.label}`}
    >
      <ExternalLink aria-hidden="true" />
      Open in Hevy
    </Button>
  ) : showPushToHevy ? (
    <Button
      variant="outline"
      size="lg"
      className="w-full px-3 text-base"
      onClick={() => onPushToHevy?.(session)}
      disabled={pushing || busy}
      data-testid="push-to-hevy"
      aria-label={pushing ? `Pushing ${session.label} to Hevy` : `Push to Hevy: ${session.label}`}
    >
      <Upload aria-hidden="true" />
      {pushing ? 'Pushing…' : 'Push to Hevy'}
    </Button>
  ) : null;

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
        <div className={cn('mt-4 grid gap-2', hasHevyButton ? TWO_UP : 'grid-cols-1')}>
          <Button asChild size="lg" className={cn('w-full', hasHevyButton && 'px-3 text-base')} data-testid="start-session">
            <Link to={`/gym/${session.liftDay}`} aria-label={`Start gym mode: ${session.label}`}>
              <Play aria-hidden="true" />
              Start
            </Link>
          </Button>
          {hevyButton}
        </div>
      )}

      {canAct && session.kind === SessionKind.Mobility && session.mobilityRoutineId && (
        <Button asChild size="lg" variant="outline" className="mt-4 w-full" data-testid="play-routine">
          <Link to={`/routines/${session.mobilityRoutineId}/play`} aria-label={`Play routine: ${session.label}`}>
            <Play aria-hidden="true" />
            Play routine
          </Link>
        </Button>
      )}

      {canAct && (session.kind === SessionKind.Cardio || session.kind === SessionKind.Mobility) && (
        <div className={cn(session.kind === SessionKind.Mobility && session.mobilityRoutineId ? 'mt-2' : 'mt-4', 'grid gap-2', hasHevyButton ? TWO_UP : 'grid-cols-1')}>
          <Button
            size="lg"
            className={cn('w-full', hasHevyButton && 'px-3 text-base')}
            onClick={() => onMarkDone?.(session)}
            disabled={busy}
            data-testid="mark-done"
          >
            <CheckCircle2 aria-hidden="true" />
            Mark done
          </Button>
          {hevyButton}
        </div>
      )}

      {isDone && (session.hevyPostStatus === 'Pending' || session.hevyPostStatus === 'Failed') && (
        <div className="mt-3 flex flex-wrap items-center gap-2 rounded-md border border-warning/50 bg-warning/10 p-2" data-testid="not-in-hevy">
          <CloudOff className="h-5 w-5 shrink-0 text-warning" aria-hidden="true" />
          <span className="min-w-0 flex-1 text-sm text-foreground">
            Not in Hevy yet{session.hevyPostStatus === 'Failed' ? ': Hevy refused it' : ''}
          </span>
          {onRetryHevy && session.gymSessionId && (
            <Button
              variant="outline"
              size="sm"
              className="h-11"
              onClick={() => onRetryHevy(session)}
              disabled={retrying}
              data-testid="retry-hevy"
              aria-label={`Retry sending ${session.label} to Hevy`}
            >
              <RefreshCw aria-hidden="true" />
              {retrying ? 'Retrying…' : 'Retry'}
            </Button>
          )}
        </div>
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
