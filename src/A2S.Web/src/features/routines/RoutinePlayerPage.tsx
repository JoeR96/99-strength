/**
 * Full-screen follow-along player for a routine at home. Big type, one step at a time: a short
 * get-ready gap, then the hold counts down (or, for reps, waits for Done). Per-side drills switch
 * sides by themselves. Works offline once the routine is loaded; finishing marks today's mobility
 * session done when this routine is on today's plan (through the offline-safe completion).
 */

import { useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { CheckCircle2, ChevronLeft, Pause, Play, SkipBack, SkipForward, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { useMobilityRoutine } from '@/hooks/useMobilityRoutines';
import { useActivePlan, useCompleteSession, useToday } from '@/hooks/usePlan';
import { useLocalToday } from '@/hooks/useLocalToday';
import { SessionKind, SessionStatus } from '@/types/plan';
import { buildSteps, drillTicks, formatDose, initialState, playerReducer, type PlayerStep } from './routinePlayer';
import { beep, primeAudio, useWakeLock } from './playerEffects';

function sideLabel(step: PlayerStep): string | null {
  if (step.side === 'left') return 'Left side';
  if (step.side === 'right') return 'Right side';
  return null;
}

function formatClock(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return m > 0 ? `${m}:${String(s).padStart(2, '0')}` : String(s);
}

export function RoutinePlayerPage() {
  const { id } = useParams<{ id: string }>();
  const { routine, isLoading } = useMobilityRoutine(id);
  if (isLoading) return <p className="p-4 text-muted-foreground">Loading…</p>;
  if (!routine) {
    return (
      <div className="space-y-3 p-4">
        <p className="text-foreground">That routine doesn't exist.</p>
        <Link className="text-primary underline" to="/routines">
          Back to routines
        </Link>
      </div>
    );
  }
  return <Player key={routine.id} routineId={routine.id} />;
}

function Player({ routineId }: { routineId: string }) {
  const navigate = useNavigate();
  const { routine } = useMobilityRoutine(routineId);
  const steps = useMemo(() => (routine ? buildSteps(routine) : []), [routine]);
  const [state, dispatch] = useReducer(playerReducer, steps, initialState);
  const [started, setStarted] = useState(false);
  const [showAlt, setShowAlt] = useState(false);

  const date = useLocalToday();
  const { data: plan } = useActivePlan();
  const { data: today } = useToday(date);
  const completeSession = useCompleteSession();
  const todaysSession = today?.sessions.find((s) => s.kind === SessionKind.Mobility && s.mobilityRoutineId === routineId);
  const alreadyDone = todaysSession?.status === SessionStatus.Done;

  const running = started && state.phase !== 'finished' && !state.paused;
  useWakeLock(started && state.phase !== 'finished');

  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => dispatch({ type: 'tick' }), 1000);
    return () => window.clearInterval(timer);
  }, [running]);

  // Sounds on transitions: start of a hold, end of the routine.
  const last = useRef({ phase: state.phase, index: state.index });
  useEffect(() => {
    const prev = last.current;
    if (started) {
      if (state.phase === 'work' && (prev.phase !== 'work' || prev.index !== state.index)) beep('go');
      else if (state.phase === 'prep' && prev.phase === 'work') beep('switch');
      else if (state.phase === 'finished' && prev.phase !== 'finished') beep('finish');
    }
    last.current = { phase: state.phase, index: state.index };
  }, [state.phase, state.index, started]);

  if (!routine) return null;
  const step = state.steps[state.index];
  const next = state.steps[state.index + 1];
  const progress = state.steps.length ? Math.round((state.index / state.steps.length) * 100) : 100;

  const markDone = () => {
    if (!plan || !todaysSession) return;
    completeSession.mutate(
      {
        planId: plan.id,
        request: {
          date,
          kind: SessionKind.Mobility,
          mobilityRoutineId: routineId,
          drillTicks: drillTicks(state, routine.drills.length),
        },
        label: todaysSession.label,
      },
      {
        onSuccess: (outcome) => {
          toast.success(outcome.queued ? `${routine.name} done — will sync when you're back online` : `${routine.name} done`);
          navigate('/today');
        },
        onError: () => toast.error("Couldn't mark that done. Try again."),
      }
    );
  };

  return (
    <div className="flex min-h-dvh flex-col bg-background pt-[max(env(safe-area-inset-top),0.75rem)] pb-[max(env(safe-area-inset-bottom),0.75rem)]">
      <header className="flex items-center justify-between gap-2 px-4">
        <Button asChild variant="ghost" className="h-11 px-2">
          <Link to="/routines" aria-label={`${routine.name}: leave the player`}>
            {state.phase === 'finished' ? <ChevronLeft aria-hidden="true" /> : <X aria-hidden="true" />}
            {routine.name}
          </Link>
        </Button>
      </header>
      <div className="mx-4 mt-2 h-1.5 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label="Routine progress" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100}>
        <div className="h-full bg-primary transition-all" style={{ width: `${state.phase === 'finished' ? 100 : progress}%` }} />
      </div>

      <main className="flex flex-1 flex-col items-center justify-center px-6 text-center">
        {!started && state.phase !== 'finished' && (
          <div className="space-y-6">
            <h1 className="text-3xl font-black text-foreground">{routine.name}</h1>
            <p className="text-muted-foreground">
              {routine.drills.length} drills · {state.steps.length} steps. Holds count down and sides switch by themselves.
            </p>
            <Button
              size="lg"
              className="h-16 w-full text-lg"
              data-testid="start-routine"
              onClick={() => {
                primeAudio();
                setStarted(true);
              }}
            >
              <Play aria-hidden="true" />
              Start
            </Button>
          </div>
        )}

        {started && step && state.phase !== 'finished' && (
          <div className="w-full max-w-md space-y-4" aria-live="polite">
            <p className="text-sm font-medium uppercase tracking-wide text-primary">
              {state.phase === 'prep' ? 'Get ready' : 'Go'}
            </p>
            <h1 className="text-3xl font-black leading-tight text-foreground sm:text-4xl">{showAlt && step.standingAlternative ? step.standingAlternative : step.drillName}</h1>
            <p className="text-lg text-foreground/90">
              {[sideLabel(step), `Set ${step.setNumber} of ${step.setsOnSide}`, formatDose(step.unit, step.amount)].filter(Boolean).join(' · ')}
            </p>
            <div className="py-4 text-7xl font-black tabular-nums text-foreground" data-testid="player-clock" aria-label={state.remaining === null ? 'Tap Done when finished' : `${state.remaining} seconds`}>
              {state.remaining === null ? '—' : formatClock(state.remaining)}
            </div>
            {step.cues && <p className="text-base text-muted-foreground">{step.cues}</p>}
            {step.standingAlternative && (
              <Button variant="ghost" className="h-11" onClick={() => setShowAlt((v) => !v)}>
                {showAlt ? 'Show the floor version' : 'Floor out? Show the standing version'}
              </Button>
            )}
            {next && (
              <p className="text-sm text-muted-foreground">
                Next: {next.drillName}
                {sideLabel(next) ? ` (${sideLabel(next)!.toLowerCase()})` : ''}
              </p>
            )}
          </div>
        )}

        {state.phase === 'finished' && (
          <div className="w-full max-w-md space-y-5">
            <CheckCircle2 className="mx-auto h-16 w-16 text-success" aria-hidden="true" />
            <h1 className="text-3xl font-black text-foreground">Routine finished</h1>
            {todaysSession && !alreadyDone && (
              <Button size="lg" className="h-14 w-full text-lg" onClick={markDone} disabled={completeSession.isPending} data-testid="mark-routine-done">
                {completeSession.isPending ? 'Saving…' : `Mark today's ${todaysSession.label} done`}
              </Button>
            )}
            {alreadyDone && <p className="text-muted-foreground">Today's {todaysSession?.label} is already marked done.</p>}
            {!todaysSession && <p className="text-muted-foreground">This routine isn't on today's plan, so nothing to mark off.</p>}
            <Button asChild variant="outline" size="lg" className="h-12 w-full">
              <Link to="/routines">Back to routines</Link>
            </Button>
          </div>
        )}
      </main>

      {started && state.phase !== 'finished' && (
        <footer className="grid grid-cols-3 gap-2 px-4">
          <Button variant="outline" className="h-14" onClick={() => dispatch({ type: 'back' })} aria-label="Previous step">
            <SkipBack aria-hidden="true" />
          </Button>
          {state.phase === 'work' && state.remaining === null ? (
            <Button className="h-14 text-lg" onClick={() => dispatch({ type: 'done' })} data-testid="step-done">
              Done
            </Button>
          ) : (
            <Button className="h-14" onClick={() => dispatch({ type: state.paused ? 'resume' : 'pause' })} aria-label={state.paused ? 'Resume' : 'Pause'} data-testid="pause-resume">
              {state.paused ? <Play aria-hidden="true" /> : <Pause aria-hidden="true" />}
            </Button>
          )}
          <Button variant="outline" className="h-14" onClick={() => dispatch({ type: state.phase === 'work' && state.remaining !== null ? 'done' : 'skip' })} aria-label="Next step">
            <SkipForward aria-hidden="true" />
          </Button>
        </footer>
      )}
    </div>
  );
}
