/**
 * One cardio exercise in gym mode, after the lifts. The stair climber (hard lane) has Start, which opens
 * the live wizard; the walk (easy lane) is one tick. Then Easy / Solid / Brutal (choosing one makes it
 * done), "Stopped early" and a pain toggle. With its starting values not set yet, Start or the tick
 * asks for them first (`CardioSettingsDialog`); once set, "Change settings" edits them. On a red day the stair
 * climber shows as skipped, with "Do it anyway".
 */

import { useState } from 'react';
import { Check, Timer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import type { Effort } from '@/types/gym';
import type { GymCardio, GymCardioChange } from './gymCardio';
import { EFFORTS, numberOrNull } from './gymInputs';
import { CardioSettingsDialog, type CardioSettingsValues } from './CardioSettingsDialog';

export interface GymCardioCardProps {
  item: GymCardio;
  workoutId: string;
  onChange: (change: GymCardioChange) => void;
  onEffort: (effort: Effort) => void;
  /** The walk's tick. */
  onTick?: () => void;
  /** The stair climber's live wizard. */
  onStart?: () => void;
  startLabel?: string;
  /** Skipped for recovery: keep it after all. */
  onKeep?: () => void;
  /** Blank starting values chosen with no signal: kept in the draft and sent with the finish. */
  onSettingsChosen?: (values: CardioSettingsValues) => void;
}

const toggleClass = (on: boolean) =>
  cn(
    'min-h-12 rounded-md border px-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
    on ? 'border-primary bg-primary text-primary-foreground' : 'border-transparent bg-foreground/[0.06] text-foreground'
  );

export function GymCardioCard({ item, workoutId, onChange, onEffort, onTick, onStart, startLabel = 'Start', onKeep, onSettingsChosen }: GymCardioCardProps) {
  const [settingsOpen, setSettingsOpen] = useState(false);
  const { block } = item;
  const id = item.exerciseId;
  const hard = item.lane === 'Hard';
  const pending = block.isSettingsPending;
  const ticked = item.totalSeconds !== undefined;
  const showLog = !pending && (hard || ticked);
  const headingId = `cardio-heading-${id}`;
  const minutes = item.totalSeconds ? Math.round((item.totalSeconds / 60) * 10) / 10 : '';

  return (
    <section className="rounded-xl border border-border bg-card p-4" aria-labelledby={headingId} data-testid={`cardio-${id}`}>
      <div className="flex items-baseline justify-between gap-2">
        <h2 id={headingId} className="text-lg font-semibold text-foreground">
          {item.name}
        </h2>
        <span className="text-sm text-muted-foreground">
          Stage {block.stageNumber} of {block.stageCount} · {block.stageName}
        </span>
      </div>
      <p className="mt-1 text-sm text-muted-foreground">{block.text}</p>
      {!pending && !item.skipped && (
        <Button type="button" variant="link" className="h-auto min-h-11 px-0 text-sm" onClick={() => setSettingsOpen(true)}>
          Change settings
        </Button>
      )}

      {item.skipped ? (
        <div className="mt-3 space-y-2">
          <p className="text-sm text-foreground">Skipped for recovery today (red readiness).</p>
          {onKeep && (
            <Button type="button" variant="outline" className="h-12 w-full" onClick={onKeep}>
              Do it anyway
            </Button>
          )}
        </div>
      ) : (
        <>
          {pending && <p className="mt-2 text-sm text-foreground">{hard ? 'Pick your hard level first.' : 'Set your speed and incline first.'}</p>}

          {hard ? (
            (pending || onStart) && (
              <Button
                type="button"
                size="lg"
                className="mt-3 h-12 w-full"
                onClick={() => (pending ? setSettingsOpen(true) : onStart?.())}
                data-testid={`cardio-start-${id}`}
              >
                <Timer aria-hidden="true" />
                {startLabel}
              </Button>
            )
          ) : (
            <button
              type="button"
              className={cn(
                'mt-3 flex min-h-14 w-full items-center gap-3 rounded-md border px-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                ticked ? 'border-transparent bg-foreground/[0.03]' : 'border-transparent bg-foreground/[0.06]'
              )}
              aria-pressed={ticked}
              onClick={() => (pending ? setSettingsOpen(true) : onTick?.())}
              data-testid={`cardio-tick-${id}`}
            >
              <span
                className={cn(
                  'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2',
                  ticked ? 'animate-tick border-success bg-success text-success-foreground' : 'border-muted-foreground/40'
                )}
                aria-hidden="true"
              >
                {ticked && <Check className="h-5 w-5" />}
              </span>
              <span className="min-w-0 flex-1 font-medium text-foreground">{pending ? 'Set up and start' : 'Done'}</span>
            </button>
          )}

          {showLog && (
            <>
              <div className={cn('mt-3 grid gap-2', hard ? 'grid-cols-2' : 'grid-cols-1')}>
                <div>
                  <Label htmlFor={`cardio-minutes-${id}`}>Total min</Label>
                  <Input
                    id={`cardio-minutes-${id}`}
                    type="number"
                    inputMode="decimal"
                    min={0}
                    className="mt-1 h-12 px-3"
                    value={minutes}
                    onChange={(e) => onChange({ totalSeconds: Math.round((numberOrNull(e.target.value) ?? 0) * 60) })}
                  />
                </div>
                {hard && (
                  <div>
                    <Label htmlFor={`cardio-floors-${id}`}>Floors</Label>
                    <Input
                      id={`cardio-floors-${id}`}
                      type="number"
                      inputMode="numeric"
                      min={0}
                      className="mt-1 h-12 px-3"
                      value={item.floors ?? ''}
                      onChange={(e) => onChange({ floors: numberOrNull(e.target.value) })}
                    />
                  </div>
                )}
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
                        item.effort === e.value ? 'border-primary bg-primary text-primary-foreground' : 'border-transparent bg-foreground/[0.06] text-foreground'
                      )}
                      aria-pressed={item.effort === e.value}
                      onClick={() => onEffort(e.value)}
                    >
                      <span className="block font-semibold">{e.value}</span>
                      <span className={cn('block text-xs', item.effort === e.value ? 'text-primary-foreground' : 'text-muted-foreground')}>{e.hint}</span>
                    </button>
                  ))}
                </div>
              </fieldset>

              <div className="mt-3 grid grid-cols-2 gap-2">
                <button type="button" className={toggleClass(!item.finished)} aria-pressed={!item.finished} onClick={() => onChange({ finished: !item.finished })}>
                  Stopped early
                </button>
                <button
                  type="button"
                  className={toggleClass(item.pain === 'Niggle')}
                  aria-pressed={item.pain === 'Niggle'}
                  onClick={() => onChange({ pain: item.pain === 'Niggle' ? 'None' : 'Niggle' })}
                >
                  Pain or niggle
                </button>
              </div>
            </>
          )}
        </>
      )}

      {settingsOpen && (
        <CardioSettingsDialog
          open
          workoutId={workoutId}
          exerciseId={id}
          name={item.name}
          block={block}
          onClose={() => setSettingsOpen(false)}
          onChosen={onSettingsChosen}
        />
      )}
    </section>
  );
}
