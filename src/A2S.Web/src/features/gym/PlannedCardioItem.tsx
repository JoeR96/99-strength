/**
 * A session's cardio finisher shown as one more planned item after the exercises: its prescription
 * ("5 min continuous @ level 6", with its easy warm-up and cool-down and the total when the track has them), progression type and "N more sessions to step up". Read-only.
 */

import { HeartPulse } from 'lucide-react';
import type { PlannedCardioDto } from '@/api/workouts';
import { FinisherParts, hasSegments } from '@/features/cardio/FinisherParts';

export function PlannedCardioItem({ cardio, compact = false }: { cardio: PlannedCardioDto; compact?: boolean }) {
  if (compact) {
    return (
      <li className="flex items-baseline justify-between gap-3 py-2.5 text-[0.9375rem]" data-testid="planned-cardio">
        <span className="flex min-w-0 items-center gap-1.5 truncate font-medium text-foreground">
          <HeartPulse className="h-4 w-4 shrink-0 text-primary" aria-hidden="true" />
          {cardio.name}
        </span>
        <span className="min-w-0 text-right text-muted-foreground">
          {hasSegments(cardio.warmUp, cardio.coolDown) ? `${cardio.text ?? cardio.prescription} · ${cardio.minutes} min` : cardio.prescription}
        </span>
      </li>
    );
  }
  return (
    <section
      className="rounded-lg border border-border bg-card p-4"
      aria-labelledby="planned-cardio-heading"
      data-testid="planned-cardio"
    >
      <div className="mb-1 flex items-baseline justify-between gap-2">
        <h2 id="planned-cardio-heading" className="flex min-w-0 items-center gap-2 truncate text-lg font-semibold text-foreground">
          <HeartPulse className="h-5 w-5 shrink-0 text-primary" aria-hidden="true" />
          {cardio.name}
        </h2>
        <span className="shrink-0 text-sm text-muted-foreground">Finisher</span>
      </div>
      <FinisherParts
        className="rounded-md bg-muted/40 px-3 py-2"
        work={cardio.prescription}
        warmUp={cardio.warmUp}
        coolDown={cardio.coolDown}
        totalMinutes={cardio.minutes}
      />
      <p className="mt-2 text-sm text-muted-foreground">
        {cardio.progressionType}
        {cardio.stepUpNote ? ` · ${cardio.stepUpNote}` : ''}
      </p>
    </section>
  );
}
