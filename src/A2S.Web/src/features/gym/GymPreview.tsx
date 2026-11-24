/**
 * Gym mode read-only (`/gym/:day?preview=1&date=…`): the same checklist order, warm-up drills, every
 * planned set with its weight and reps and the AMRAP target, each cardio exercise's block, and the finisher's prescription with
 * "N more sessions to step up". Nothing can be ticked; no draft is saved, no wake lock, no rest timer.
 * Start appears only when the previewed day is today.
 */

import { FinisherParts } from '@/features/cardio/FinisherParts';
import { Link } from 'react-router-dom';
import { Eye, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PlannedCardioDto } from '@/api/workouts';
import { PlannedCardioItem } from './PlannedCardioItem';
import { cardioBlockLine } from './gymCardio';
import { liftBlocks, type GymLift, type GymSessionState, type GymSet } from './gymSession';

export interface GymPreviewProps {
  state: GymSessionState;
  /** "Tomorrow · Tue 24 Nov". */
  dayLabel: string;
  /** Where Start goes, or null when the day is not today. */
  startHref: string | null;
  /** The finisher as the week plan's planned item (prescription, progression, step-up). Preferred over `state.finisher`. */
  cardio?: PlannedCardioDto | null;
}

function weight(set: GymSet, unit: string, lift?: GymLift): string {
  if (lift?.trainingMaxPending && set.plannedWeight === 0) return lift.intensity ? `${Math.round(lift.intensity * 100)} %` : 'TM not set';
  return `${Number.isInteger(set.plannedWeight) ? set.plannedWeight : set.plannedWeight.toFixed(1)} ${unit}`;
}

export function GymPreview({ state, dayLabel, startHref, cardio }: GymPreviewProps) {
  const supersetLabels: (string | null)[] = state.lifts.map(() => null);
  for (const block of liftBlocks(state)) block.lifts.forEach((l, k) => (supersetLabels[l] = block.labels[k]));
  return (
    <main className="mx-auto max-w-2xl space-y-4 px-4 py-6" data-testid="gym-preview">
      <header>
        <p className="text-sm text-muted-foreground">
          Preview · {dayLabel} · Week {state.week}
        </p>
        <h1 className="text-2xl font-semibold text-foreground">{state.title}</h1>
      </header>

      <p
        className="flex items-start gap-2 rounded-md border border-border bg-muted/40 p-3 text-sm text-foreground"
        role="note"
        data-testid="preview-banner"
      >
        <Eye className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        Preview: weights can change if you finish sessions before then.
      </p>

      {state.drills.length > 0 && (
        <section
          className="rounded-lg border border-border bg-card p-4"
          aria-labelledby="preview-warm-up"
        >
          <h2 id="preview-warm-up" className="mb-3 text-lg font-semibold text-foreground">
            Warm-up
          </h2>
          <ul className="space-y-2">
            {state.drills.map((drill, i) => (
              <li key={`${drill.name}-${i}`} className="flex justify-between gap-3 text-sm">
                <span className="min-w-0 truncate font-medium text-foreground">{drill.name}</span>
                <span className="shrink-0 text-muted-foreground">{drill.dose}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {state.lifts.map((lift, li) => (
        <section
          key={lift.exerciseId}
          className="rounded-lg border border-border bg-card p-4"
          aria-labelledby={`preview-lift-${li}`}
          data-testid={`preview-lift-${li}`}
        >
          <div className="mb-3 flex items-baseline justify-between gap-2">
            <h2
              id={`preview-lift-${li}`}
              className="min-w-0 truncate text-lg font-semibold text-foreground"
            >
              {supersetLabels[li] ? `${supersetLabels[li]} ` : ''}
              {lift.name}
            </h2>
            <span className="shrink-0 text-sm text-muted-foreground">{lift.sets.length} sets</span>
          </div>
          <ol className="space-y-1">
            {lift.sets.map((set) => (
              <li
                key={set.setNumber}
                className="flex justify-between gap-3 rounded-md bg-muted/40 px-3 py-2"
              >
                <span className="text-sm text-muted-foreground">
                  Set {set.setNumber}
                  {set.isAmrap && ' · AMRAP'}
                </span>
                <span className="font-semibold text-foreground">
                  {weight(set, lift.unit, lift)} × {set.isAmrap ? `${set.plannedReps}+` : set.plannedReps}
                </span>
              </li>
            ))}
          </ol>
          {lift.extraSet && (
            <p className="mt-2 text-sm text-muted-foreground">Extra set: +1 {lift.extraSet.side.toLowerCase()}.</p>
          )}
          {lift.trainingMaxPending && (
            <p className="mt-2 text-sm text-foreground">Training max not set yet: set it on the Workout page or in gym mode.</p>
          )}
          {lift.sets.some((s) => s.isAmrap) && (
            <p className="mt-2 text-sm text-muted-foreground">
              AMRAP target: {lift.sets.find((s) => s.isAmrap)!.plannedReps}+ reps on the last set.
            </p>
          )}
        </section>
      ))}

      {state.cardio.map((item) => (
        <section
          key={item.exerciseId}
          className="rounded-lg border border-border bg-card p-4"
          aria-labelledby={`preview-cardio-${item.exerciseId}`}
          data-testid={`preview-cardio-${item.exerciseId}`}
        >
          <h2 id={`preview-cardio-${item.exerciseId}`} className="text-lg font-semibold text-foreground">
            {item.name}
          </h2>
          <p className="mt-1 text-sm text-foreground">{cardioBlockLine(item.block)}</p>
        </section>
      ))}

      {cardio && <PlannedCardioItem cardio={cardio} />}

      {!cardio && state.finisher && (
        <section
          className="rounded-lg border border-border bg-card p-4"
          aria-labelledby="preview-finisher"
          data-testid="preview-finisher"
        >
          <h2 id="preview-finisher" className="text-lg font-semibold text-foreground">
            Finisher: {state.finisher.name}
          </h2>
          <FinisherParts
            className="mt-1 text-sm"
            work={state.finisher.prescription}
            warmUp={state.finisher.warmUp}
            coolDown={state.finisher.coolDown}
            totalMinutes={state.finisher.minutes}
          />
          {state.finisher.stepUp && (
            <p className="text-sm text-foreground">{state.finisher.stepUp}</p>
          )}
        </section>
      )}

      <div className="space-y-2 pb-8 pt-2">
        {startHref && (
          <Button asChild size="lg" className="w-full" data-testid="preview-start">
            <Link to={startHref}>
              <Play aria-hidden="true" />
              Start
            </Link>
          </Button>
        )}
        <Button asChild variant="outline" size="lg" className="w-full">
          <Link to="/today">Back to workout</Link>
        </Button>
      </div>
    </main>
  );
}
