/**
 * A follow-along routine (a video or another app's session): Start opens the link in a new tab, and the
 * "I'm back — log it" screen records the minutes actually done (default: the expected minutes). Completion
 * goes through the offline-safe session completion and posts to Hevy as Yoga on the server.
 */

import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ExternalLink, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useActivePlan, useCompleteSession, useToday } from '@/hooks/usePlan';
import { useLocalToday } from '@/hooks/useLocalToday';
import { SessionKind, SessionStatus } from '@/types/plan';
import type { MobilityRoutineDto } from '@/types/mobility';

export function FollowAlongPlayer({ routine, initiallyBack = false }: { routine: MobilityRoutineDto; initiallyBack?: boolean }) {
  const navigate = useNavigate();
  const [back, setBack] = useState(initiallyBack);
  const [minutes, setMinutes] = useState(String(routine.targetMinutes));
  const date = useLocalToday();
  const { data: plan } = useActivePlan();
  const { data: today } = useToday(date);
  const completeSession = useCompleteSession();
  const todaysSession = today?.sessions.find((s) => s.kind === SessionKind.Mobility && s.mobilityRoutineId === routine.id);
  const alreadyDone = todaysSession?.status === SessionStatus.Done;
  const parsed = /^\d+$/.test(minutes.trim()) ? Number(minutes) : NaN;
  const minutesValid = parsed >= 1 && parsed <= 600;

  const start = () => {
    if (routine.followAlongUrl) window.open(routine.followAlongUrl, '_blank', 'noopener,noreferrer');
    setBack(true);
  };

  const log = () => {
    if (!plan || !todaysSession || !minutesValid) return;
    completeSession.mutate(
      {
        planId: plan.id,
        request: { date, kind: SessionKind.Mobility, mobilityRoutineId: routine.id, durationMinutes: parsed },
        label: todaysSession.label,
      },
      {
        onSuccess: (outcome) => {
          toast.success(outcome.queued ? `${routine.name} logged — will sync when you're back online` : `${routine.name} logged`);
          navigate('/today');
        },
        onError: () => toast.error("Couldn't log that. Try again."),
      }
    );
  };

  return (
    <div className="flex min-h-dvh flex-col bg-background pt-[max(env(safe-area-inset-top),0.75rem)] pb-[max(env(safe-area-inset-bottom),0.75rem)]">
      <header className="flex items-center px-4">
        <Button asChild variant="ghost" className="h-11 px-2">
          <Link to="/routines" aria-label={`${routine.name}: leave`}>
            <X aria-hidden="true" />
            {routine.name}
          </Link>
        </Button>
      </header>
      <main className="flex flex-1 flex-col items-center justify-center px-6 text-center">
        {!back ? (
          <div className="w-full max-w-md space-y-6">
            <h1 className="text-3xl font-black text-foreground">{routine.name}</h1>
            <p className="text-muted-foreground">About {routine.targetMinutes} min. It opens in a new tab; come back here to log it.</p>
            <Button size="lg" className="h-16 w-full text-lg" onClick={start} data-testid="start-follow-along">
              <ExternalLink aria-hidden="true" />
              Start
            </Button>
          </div>
        ) : (
          <div className="w-full max-w-md space-y-5 text-left">
            <h1 className="text-center text-3xl font-black text-foreground">I'm back — log it</h1>
            <div>
              <label htmlFor="follow-minutes" className="mb-1 block text-sm font-medium text-foreground">
                Minutes done
              </label>
              <Input
                id="follow-minutes"
                inputMode="numeric"
                value={minutes}
                aria-invalid={!minutesValid}
                aria-describedby="follow-minutes-hint"
                onChange={(e) => setMinutes(e.target.value)}
              />
              <p id="follow-minutes-hint" className="mt-1 text-sm text-muted-foreground">
                {minutesValid ? 'Logged in Hevy as Yoga.' : 'Between 1 and 600 minutes'}
              </p>
            </div>
            {todaysSession && !alreadyDone && (
              <Button size="lg" className="h-14 w-full text-lg" onClick={log} disabled={completeSession.isPending || !minutesValid} data-testid="log-follow-along">
                {completeSession.isPending ? 'Saving…' : `Log ${minutesValid ? parsed : ''} min`}
              </Button>
            )}
            {alreadyDone && <p className="text-muted-foreground">Today's {todaysSession?.label} is already logged.</p>}
            {!todaysSession && <p className="text-muted-foreground">This routine isn't on today's plan, so nothing to log against.</p>}
            {routine.followAlongUrl && (
              <Button asChild variant="outline" size="lg" className="h-12 w-full">
                <a href={routine.followAlongUrl} target="_blank" rel="noopener noreferrer">
                  <ExternalLink aria-hidden="true" />
                  Open the video again
                </a>
              </Button>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
