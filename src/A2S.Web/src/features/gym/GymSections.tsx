/** The warm-up list, the finisher card, the rest timer bar and the Hevy status line of gym mode. */

import { Check, CloudOff, RefreshCw, Timer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import type { Effort } from '@/types/gym';
import type { HevyPostStatus } from '@/types/plan';
import type { GymDrill, GymFinisher } from './gymSession';
import { formatClock } from './useRestTimer';
import { FinisherParts, hasSegments } from '@/features/cardio/FinisherParts';

export function WarmUpSection({
  drills,
  onTick,
  onAllDone,
}: {
  drills: GymDrill[];
  onTick: (index: number) => void;
  onAllDone: () => void;
}) {
  const done = drills.filter((d) => d.done).length;
  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby="warm-up-heading">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 id="warm-up-heading" className="text-lg font-semibold text-foreground">
          Warm-up
        </h2>
        <span className="text-sm text-muted-foreground">
          {done}/{drills.length}
        </span>
      </div>
      <ul className="space-y-2">
        {drills.map((drill, i) => (
          <li key={`${drill.name}-${i}`}>
            <button
              type="button"
              className={cn(
                'flex min-h-12 w-full items-center gap-3 rounded-md border px-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                drill.done ? 'border-success/60 bg-success/15' : 'border-border bg-muted/40'
              )}
              aria-pressed={drill.done}
              onClick={() => onTick(i)}
              data-testid={`drill-${i}`}
            >
              <span
                className={cn(
                  'flex h-7 w-7 shrink-0 items-center justify-center rounded-full border-2',
                  drill.done ? 'border-success bg-success text-success-foreground' : 'border-muted-foreground/40'
                )}
                aria-hidden="true"
              >
                {drill.done && <Check className="h-4 w-4" />}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium text-foreground">{drill.name}</span>
                <span className="block text-sm text-muted-foreground">{drill.dose}</span>
              </span>
            </button>
          </li>
        ))}
      </ul>
      {done < drills.length && (
        <Button type="button" variant="outline" className="mt-3 h-12 w-full" onClick={onAllDone} data-testid="warm-up-all-done">
          All done
        </Button>
      )}
    </section>
  );
}

const EFFORTS: { value: Effort; hint: string }[] = [
  { value: 'Easy', hint: 'could chat' },
  { value: 'Solid', hint: 'hard but steady' },
  { value: 'Brutal', hint: 'all out' },
];

function numberOrNull(text: string): number | null {
  if (text.trim() === '') return null;
  const value = Number(text);
  return Number.isFinite(value) && value >= 0 ? value : null;
}

export function FinisherCard({
  finisher,
  onChange,
}: {
  finisher: GymFinisher;
  onChange: (change: Partial<Pick<GymFinisher, 'minutes' | 'workMinutes' | 'level' | 'floors' | 'effort' | 'pain'>>) => void;
}) {
  const segmented = hasSegments(finisher.warmUp, finisher.coolDown);
  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby="finisher-heading" data-testid="gym-finisher">
      <h2 id="finisher-heading" className="text-lg font-semibold text-foreground">
        Finisher: {finisher.name}
      </h2>
      {segmented ? (
        <div className="mt-1" data-testid="finisher-prescription">
          <FinisherParts work={finisher.prescription} warmUp={finisher.warmUp} coolDown={finisher.coolDown} totalMinutes={finisher.minutes} />
        </div>
      ) : (
        <p className="mt-1 text-sm text-muted-foreground" data-testid="finisher-prescription">{finisher.prescription}</p>
      )}
      {finisher.stepUp && <p className="text-sm text-foreground">{finisher.stepUp}</p>}

      {segmented && (
        <div className="mt-3">
          <Label htmlFor="finisher-work-minutes">Work minutes (hard part)</Label>
          <Input
            id="finisher-work-minutes"
            type="number"
            inputMode="decimal"
            min={0}
            className="mt-1 h-12 px-3"
            value={finisher.workMinutes ?? ''}
            onChange={(e) => onChange({ workMinutes: numberOrNull(e.target.value) })}
          />
        </div>
      )}

      <div className="mt-3 grid grid-cols-3 gap-2">
        <div>
          <Label htmlFor="finisher-minutes">{segmented ? 'Total min' : 'Minutes'}</Label>
          <Input id="finisher-minutes" type="number" inputMode="decimal" min={0} className="mt-1 h-12 px-3" value={finisher.minutes ?? ''} onChange={(e) => onChange({ minutes: numberOrNull(e.target.value) })} />
        </div>
        <div>
          <Label htmlFor="finisher-level">Level</Label>
          <Input id="finisher-level" type="number" inputMode="numeric" min={1} className="mt-1 h-12 px-3" value={finisher.level ?? ''} onChange={(e) => onChange({ level: numberOrNull(e.target.value) })} />
        </div>
        <div>
          <Label htmlFor="finisher-floors">Floors</Label>
          <Input id="finisher-floors" type="number" inputMode="numeric" min={0} className="mt-1 h-12 px-3" value={finisher.floors ?? ''} onChange={(e) => onChange({ floors: numberOrNull(e.target.value) })} />
        </div>
      </div>

      <fieldset className="mt-3">
        <legend className="text-sm font-medium text-foreground">How did it feel?</legend>
        <div className="mt-1 grid grid-cols-3 gap-2">
          {EFFORTS.map((e) => (
            <button
              key={e.value}
              type="button"
              className={cn(
                'min-h-14 rounded-md border px-2 text-center focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                finisher.effort === e.value ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-muted/40 text-foreground'
              )}
              aria-pressed={finisher.effort === e.value}
              onClick={() => onChange({ effort: e.value })}
            >
              <span className="block font-semibold">{e.value}</span>
              <span className={cn('block text-xs', finisher.effort === e.value ? 'text-primary-foreground' : 'text-muted-foreground')}>{e.hint}</span>
            </button>
          ))}
        </div>
      </fieldset>

      {finisher.trackId && (
        <fieldset className="mt-3">
          <legend className="text-sm font-medium text-foreground">Any pain?</legend>
          <div className="mt-1 grid grid-cols-3 gap-2">
            {(['None', 'Niggle', 'Stop'] as const).map((value) => (
              <button
                key={value}
                type="button"
                className={cn(
                  'min-h-12 rounded-md border px-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  (finisher.pain ?? 'None') === value ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-muted/40 text-foreground'
                )}
                aria-pressed={(finisher.pain ?? 'None') === value}
                onClick={() => onChange({ pain: value })}
              >
                {value === 'None' ? 'No pain' : value === 'Stop' ? 'Had to stop' : 'Niggle'}
              </button>
            ))}
          </div>
        </fieldset>
      )}
    </section>
  );
}

/** A fixed cool-down (treadmill, easy incline, 10 min): one tick, logged as done as prescribed. */
export function FixedFinisherCard({ finisher, onToggle }: { finisher: GymFinisher; onToggle: () => void }) {
  const done = finisher.done === true;
  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby="finisher-heading" data-testid="gym-finisher">
      <h2 id="finisher-heading" className="mb-3 text-lg font-semibold text-foreground">
        Cool-down
      </h2>
      <button
        type="button"
        className={cn(
          'flex min-h-14 w-full items-center gap-3 rounded-md border px-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          done ? 'border-success/60 bg-success/15' : 'border-border bg-muted/40'
        )}
        aria-pressed={done}
        onClick={onToggle}
        data-testid="fixed-finisher"
      >
        <span
          className={cn(
            'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2',
            done ? 'border-success bg-success text-success-foreground' : 'border-muted-foreground/40'
          )}
          aria-hidden="true"
        >
          {done && <Check className="h-5 w-5" />}
        </span>
        <span className="min-w-0 flex-1 font-medium text-foreground" data-testid="finisher-prescription">
          {finisher.prescription}
        </span>
      </button>
    </section>
  );
}

export function RestTimerBar({
  remaining,
  total,
  onAdjust,
  onSkip,
}: {
  remaining: number;
  total: number;
  onAdjust: (seconds: number) => void;
  onSkip: () => void;
}) {
  const percent = total > 0 ? Math.round((remaining / total) * 100) : 0;
  return (
    <div className="sticky top-0 z-20 border-b border-border bg-card/95 px-4 py-2 backdrop-blur" role="region" aria-label="Rest timer" data-testid="rest-timer">
      <div className="mx-auto flex max-w-2xl items-center gap-2">
        <Timer className="h-5 w-5 shrink-0 text-primary" aria-hidden="true" />
        <span className="min-w-0 flex-1 text-2xl font-semibold tabular-nums text-foreground" role="timer" aria-label={`Rest: ${formatClock(remaining)} left`}>
          {formatClock(remaining)}
        </span>
        <Button type="button" variant="outline" size="sm" className="h-11 px-3" onClick={() => onAdjust(-15)} aria-label="15 seconds less rest">
          −15
        </Button>
        <Button type="button" variant="outline" size="sm" className="h-11 px-3" onClick={() => onAdjust(15)} aria-label="15 seconds more rest">
          +15
        </Button>
        <Button type="button" size="sm" className="h-11 px-3" onClick={onSkip}>
          Skip
        </Button>
      </div>
      <div className="mx-auto mt-2 h-1 max-w-2xl overflow-hidden rounded bg-muted" aria-hidden="true">
        <div className="h-full bg-primary transition-[width]" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}

/** Where a finished session stands in Hevy, with Retry while it is not there yet. */
export function GymHevyStatus({
  status,
  queued = false,
  onRetry,
  retrying = false,
}: {
  status: HevyPostStatus | null;
  queued?: boolean;
  onRetry?: () => void;
  retrying?: boolean;
}) {
  if (status === 'Posted') {
    return (
      <div className="mb-6 flex items-center gap-2 rounded-lg border border-success/60 bg-success/10 p-4 text-foreground" data-testid="gym-hevy-status">
        <Check className="h-5 w-5 text-success" aria-hidden="true" />
        <span>In Hevy</span>
      </div>
    );
  }
  if (status === 'Skipped' || status === null) {
    if (!queued) return null;
  }
  const text = queued
    ? 'Saved on this phone. It finishes and goes to Hevy when you are back online.'
    : status === 'Failed'
      ? 'Not in Hevy yet: Hevy refused it. Check your key in Settings, then retry.'
      : 'Not in Hevy yet. It keeps trying every few minutes.';
  return (
    <div className="mb-6 flex flex-wrap items-center gap-3 rounded-lg border border-warning/50 bg-warning/10 p-4" data-testid="gym-hevy-status">
      <CloudOff className="h-5 w-5 shrink-0 text-warning" aria-hidden="true" />
      <span className="min-w-0 flex-1 text-sm text-foreground">{text}</span>
      {!queued && onRetry && (
        <Button type="button" variant="outline" className="h-11" onClick={onRetry} disabled={retrying}>
          <RefreshCw aria-hidden="true" />
          {retrying ? 'Retrying…' : 'Retry'}
        </Button>
      )}
    </div>
  );
}
