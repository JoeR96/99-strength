/** Hard sets per muscle group this week against the cap, with the API's warnings. */

import { AlertTriangle, Clock3 } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { DayBudgetDto } from '@/types/budget';
import { volumeBars } from './volume';

export interface VolumeThisWeekProps {
  budget: DayBudgetDto | undefined;
  loading?: boolean;
  error?: boolean;
}

export function VolumeThisWeek({ budget, loading = false, error = false }: VolumeThisWeekProps) {
  const bars = volumeBars(budget);

  return (
    <section aria-labelledby="volume-heading" className="rounded-lg border border-border bg-card p-4" data-testid="volume-this-week">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 id="volume-heading" className="text-sm font-medium text-muted-foreground">
          Hard sets this week
        </h2>
        {budget && (
          <span className={cn('flex items-center gap-1 text-xs tabular-nums', budget.time.overBudget ? 'text-warning' : 'text-muted-foreground')}>
            <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
            Next session ~{budget.time.totalMinutes} min
            {budget.time.overBudget && ` (over ${budget.time.budgetMinutes})`}
          </span>
        )}
      </div>

      {loading && <div className="h-40 animate-pulse rounded-md bg-muted" aria-hidden="true" />}
      {error && (
        <p className="text-sm text-muted-foreground" role="alert">
          Couldn't load this week's volume.
        </p>
      )}
      {!loading && !error && bars.length === 0 && (
        <p className="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
          No lifting programme yet, so nothing to count.
        </p>
      )}

      {bars.length > 0 && (
        <ul className="space-y-2">
          {bars.map((bar) => (
            <li key={bar.bucket} className="grid grid-cols-[6rem_1fr_3.5rem] items-center gap-2 text-sm" data-testid={`volume-${bar.bucket}`}>
              <span className="truncate text-foreground">{bar.bucket}</span>
              <div
                className="relative h-3 overflow-hidden rounded-full bg-muted"
                role="progressbar"
                aria-label={`${bar.bucket}: ${bar.sets} of ${bar.cap ?? '?'} hard sets`}
                aria-valuenow={bar.sets}
                aria-valuemin={0}
                aria-valuemax={bar.cap ?? undefined}
              >
                <div className={cn('h-full rounded-full', bar.over ? 'bg-warning' : 'bg-primary')} style={{ width: `${bar.fill * 100}%` }} />
              </div>
              <span className={cn('text-right tabular-nums', bar.over ? 'font-medium text-warning' : 'text-muted-foreground')}>
                {bar.sets}
                {bar.cap !== null && <span className="text-muted-foreground">/{bar.cap}</span>}
              </span>
            </li>
          ))}
        </ul>
      )}

      {budget && budget.volumeWarnings.length > 0 && (
        <ul className="mt-3 space-y-1" aria-label="Volume warnings">
          {budget.volumeWarnings.map((w) => (
            <li key={w.bucket} className="flex items-start gap-2 text-xs text-warning">
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              {w.message}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
