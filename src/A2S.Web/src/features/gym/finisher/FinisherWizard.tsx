/**
 * The live stair-finisher wizard (gym mode, full screen): the current phase, its level and zone, a
 * big countdown, phase and session progress, "Next: 1 min hard @ L6", the round counter and the
 * controls (back, −30 s, pause, +30 s, skip), all at least 44 px. Beeps 3-2-1 and at each phase
 * change, optionally speaks the cue, holds a screen wake lock and runs offline. Everything is derived
 * from the wall clock, so switching to YouTube and back (or a reload) lands on the right phase.
 * Finishing shows a summary that prefills the finisher log; abandoning logs nothing.
 */

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Minus, Pause, Play, Plus, SkipBack, SkipForward, Volume2, VolumeX, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { beep, useWakeLock } from '@/features/routines/playerEffects';
import { formatClock } from '../useRestTimer';
import { durationText, nextText, phaseCue, type FinisherPhase } from './finisherPlan';
import { summaryOf, type FinisherEngineState, type FinisherSummary, type ScheduledNudge } from './finisherEngine';
import { useFinisherEngine } from './useFinisherEngine';

const SPEAK_KEY = 'a2s.finisher.speak';

function readSpeak(): boolean {
  try {
    return localStorage.getItem(SPEAK_KEY) === '1';
  } catch {
    return false;
  }
}

function speak(text: string): void {
  try {
    if (!('speechSynthesis' in window)) return;
    window.speechSynthesis.cancel();
    window.speechSynthesis.speak(new SpeechSynthesisUtterance(text));
  } catch {
    /* speech is a nicety */
  }
}

export interface FinisherWizardProps {
  name: string;
  clientSessionId: string;
  phases: FinisherPhase[];
  /** Called after each tap with the phase-change schedule (the server notifies while the page is asleep). */
  onSchedule?: (nudges: ScheduledNudge[]) => void;
  /** The summary's "Log finisher": prefill the log and go back to the checklist. */
  onDone: (summary: FinisherSummary) => void;
  /** Abandon: back to the card, nothing logged. */
  onAbandon: () => void;
  /** Stories/tests. */
  clock?: () => number;
  initial?: FinisherEngineState | null;
  initialSpeak?: boolean;
}

function zoneText(phase: FinisherPhase): string | null {
  const parts = [phase.level != null ? `Level ${phase.level}` : null, phase.zone ? (/^z/i.test(phase.zone) ? phase.zone.toUpperCase() : `Zone ${phase.zone}`) : null].filter(Boolean);
  return parts.length ? parts.join(' · ') : null;
}

function Bar({ value, label, thick = false }: { value: number; label: string; thick?: boolean }) {
  const pct = Math.round(Math.max(0, Math.min(1, value)) * 100);
  return (
    <div role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct} className={cn('w-full overflow-hidden rounded-full bg-muted', thick ? 'h-4' : 'h-2')}>
      <div className="h-full rounded-full bg-primary transition-[width] duration-200" style={{ width: `${pct}%` }} />
    </div>
  );
}

export function FinisherWizard({ name, clientSessionId, phases, onSchedule, onDone, onAbandon, clock, initial, initialSpeak }: FinisherWizardProps) {
  const engine = useFinisherEngine({ clientSessionId, phases, clock, initial, onSchedule });
  const { view, state } = engine;
  const [speakOn, setSpeakOn] = useState(() => initialSpeak ?? readSpeak());
  const [confirmAbandon, setConfirmAbandon] = useState(false);
  useWakeLock(!!view && !view.finished);

  // 3-2-1 and the phase-change beep (plus the spoken cue). Only one beep when several phases passed in the background.
  const last = useRef<{ index: number; sec: number } | null>(null);
  useEffect(() => {
    if (!view) return;
    const sec = Math.ceil(view.remainingMs / 1000);
    const prev = last.current;
    last.current = { index: view.index, sec };
    if (!prev) return;
    if (view.index !== prev.index) {
      beep(view.finished ? 'finish' : 'go');
      navigator.vibrate?.(view.finished ? 400 : 200);
      if (speakOn) speak(view.finished ? 'Finisher done' : view.phase ? phaseCue(view.phase) : '');
    } else if (!view.paused && sec !== prev.sec && sec >= 1 && sec <= 3) {
      beep('switch');
    }
  }, [view, speakOn]);

  const toggleSpeak = () => {
    const next = !speakOn;
    setSpeakOn(next);
    try {
      localStorage.setItem(SPEAK_KEY, next ? '1' : '0');
    } catch {
      /* ignore */
    }
  };

  if (!view || !state) {
    return (
      <Shell name={name} onClose={onAbandon} speakOn={speakOn} onToggleSpeak={toggleSpeak}>
        <p className="text-center text-muted-foreground">Ready when you are: {phases.length} phases.</p>
        <Button size="lg" className="h-14 w-full" onClick={engine.start} data-testid="finisher-wizard-start">
          <Play aria-hidden="true" /> Start
        </Button>
      </Shell>
    );
  }

  if (view.finished) {
    const summary = summaryOf(state, engine.now);
    return (
      <Shell name={name} onClose={onAbandon} speakOn={speakOn} onToggleSpeak={toggleSpeak} hideClose>
        <div className="space-y-4 text-center" data-testid="finisher-wizard-summary">
          <h2 className="text-3xl font-semibold text-foreground">Finisher done</h2>
          <dl className="grid grid-cols-2 gap-3 text-left">
            <Stat label="Total" value={durationText(summary.totalSeconds)} />
            <Stat label="Work" value={durationText(summary.workSeconds)} />
            <Stat label="Level" value={summary.level != null ? String(summary.level) : '—'} />
            {summary.rounds > 0 ? <Stat label="Rounds" value={String(summary.rounds)} /> : <Stat label="Floors" value="Add on the log" />}
          </dl>
          <p className="text-sm text-muted-foreground">Add your floors and how it felt on the finisher card.</p>
          <Button size="lg" className="h-14 w-full" onClick={() => onDone(summary)} data-testid="finisher-wizard-log">
            Log finisher
          </Button>
        </div>
      </Shell>
    );
  }

  const phase = view.phase!;
  const zone = zoneText(phase);
  const sec = Math.ceil(view.remainingMs / 1000);
  return (
    <Shell name={name} onClose={() => setConfirmAbandon(true)} speakOn={speakOn} onToggleSpeak={toggleSpeak}>
      <div className="space-y-3 text-center" aria-live="polite">
        <p className="text-sm text-muted-foreground">
          Phase {view.index + 1} of {state.phases.length}
          {phase.round ? ` · Round ${phase.round} of ${phase.rounds}` : ''}
        </p>
        <h2 className="text-5xl font-semibold text-foreground" data-testid="finisher-wizard-phase">
          {phase.label}
        </h2>
        {zone && <p className="text-2xl font-medium text-primary">{zone}</p>}
      </div>

      <div className="space-y-3">
        <p
          className={cn('text-center font-semibold tabular-nums text-foreground', 'text-8xl', !view.paused && sec <= 3 && 'text-primary')}
          data-testid="finisher-wizard-countdown"
          aria-label={`${formatClock(sec)} left`}
        >
          {formatClock(sec)}
        </p>
        <Bar value={view.phaseProgress} label="Phase progress" thick />
        {view.paused && <p className="text-center text-lg font-semibold text-foreground">Paused</p>}
      </div>

      <div className="space-y-2">
        <p className="text-center text-lg text-foreground" data-testid="finisher-wizard-next">
          {view.next ? `Next: ${nextText(view.next)}` : 'Last phase'}
        </p>
        <Bar value={view.sessionProgress} label="Whole finisher" />
      </div>

      {confirmAbandon ? (
        <div className="space-y-2 rounded-lg border border-border bg-card p-4" role="alertdialog" aria-labelledby="finisher-abandon-title">
          <p id="finisher-abandon-title" className="font-semibold text-foreground">Stop the finisher? Nothing is logged.</p>
          <div className="grid grid-cols-2 gap-2">
            <Button variant="outline" className="h-12" onClick={() => setConfirmAbandon(false)}>
              Keep going
            </Button>
            <Button variant="destructive" className="h-12" onClick={onAbandon} data-testid="finisher-wizard-abandon">
              Stop
            </Button>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-5 gap-2">
          <Control label="Back" onClick={engine.back}>
            <SkipBack aria-hidden="true" />
          </Control>
          <Control label="30 seconds less" onClick={() => engine.adjust(-30)}>
            <Minus aria-hidden="true" />
            <span className="text-xs">30</span>
          </Control>
          <Control label={view.paused ? 'Resume' : 'Pause'} onClick={view.paused ? engine.resume : engine.pause} primary>
            {view.paused ? <Play aria-hidden="true" /> : <Pause aria-hidden="true" />}
          </Control>
          <Control label="30 seconds more" onClick={() => engine.adjust(30)}>
            <Plus aria-hidden="true" />
            <span className="text-xs">30</span>
          </Control>
          <Control label="Skip phase" onClick={engine.skip}>
            <SkipForward aria-hidden="true" />
          </Control>
        </div>
      )}
    </Shell>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-xl font-semibold text-foreground">{value}</dd>
    </div>
  );
}

function Control({ label, onClick, primary = false, children }: { label: string; onClick: () => void; primary?: boolean; children: ReactNode }) {
  return (
    <Button type="button" variant={primary ? 'default' : 'outline'} className="flex h-16 min-w-11 flex-col gap-0 px-1" aria-label={label} onClick={onClick}>
      {children}
    </Button>
  );
}

function Shell({
  name,
  onClose,
  speakOn,
  onToggleSpeak,
  hideClose = false,
  children,
}: {
  name: string;
  onClose: () => void;
  speakOn: boolean;
  onToggleSpeak: () => void;
  hideClose?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-background" role="dialog" aria-modal="true" aria-labelledby="finisher-wizard-title" data-testid="finisher-wizard">
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-between gap-6 px-4 py-6">
        <header className="flex items-center justify-between gap-2">
          <h1 id="finisher-wizard-title" className="min-w-0 truncate text-xl font-semibold text-foreground">
            {name}
          </h1>
          <div className="flex shrink-0 gap-2">
            <Button type="button" variant="ghost" className="h-11 w-11 p-0" aria-pressed={speakOn} aria-label={speakOn ? 'Spoken cues on' : 'Spoken cues off'} onClick={onToggleSpeak}>
              {speakOn ? <Volume2 aria-hidden="true" /> : <VolumeX aria-hidden="true" />}
            </Button>
            {!hideClose && (
              <Button type="button" variant="ghost" className="h-11 w-11 p-0" aria-label="Abandon finisher" onClick={onClose}>
                <X aria-hidden="true" />
              </Button>
            )}
          </div>
        </header>
        {children}
      </main>
    </div>
  );
}
