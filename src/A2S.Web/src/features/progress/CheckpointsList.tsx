/** The plan's checkpoints: date, target, note, and how the trend compared on days already passed. */

import { Check, Flag } from 'lucide-react';
import { cn } from '@/lib/utils';
import { compareLocalDates, formatShortDate, type LocalDate } from '@/lib/localDate';
import type { TrendPointDto } from '@/types/bodyWeight';
import type { CheckpointDto } from '@/types/plan';
import { formatDelta } from './trend';

export interface CheckpointsListProps {
  checkpoints: CheckpointDto[];
  points: TrendPointDto[];
  today: LocalDate;
}

function trendOnOrBefore(points: TrendPointDto[], date: LocalDate): TrendPointDto | undefined {
  let best: TrendPointDto | undefined;
  for (const p of points) {
    if (compareLocalDates(p.date, date) <= 0 && (!best || compareLocalDates(p.date, best.date) > 0)) best = p;
  }
  return best;
}

export function CheckpointsList({ checkpoints, points, today }: CheckpointsListProps) {
  if (checkpoints.length === 0) return null;
  const sorted = [...checkpoints].sort((a, b) => compareLocalDates(a.date, b.date));

  return (
    <section aria-labelledby="checkpoints-heading" className="rounded-lg border border-border bg-card p-4" data-testid="checkpoints">
      <h2 id="checkpoints-heading" className="mb-3 text-sm font-medium text-muted-foreground">
        Checkpoints
      </h2>
      <ol className="space-y-2">
        {sorted.map((c) => {
          const passed = compareLocalDates(c.date, today) <= 0;
          const at = passed ? trendOnOrBefore(points, c.date) : undefined;
          const delta = at ? at.ewma - c.targetKg : null;
          const hit = delta !== null && delta <= 0.25;
          return (
            <li key={c.date} className="flex items-center gap-3 text-sm" data-testid={`checkpoint-${c.date}`}>
              <span
                className={cn(
                  'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border',
                  passed ? (hit ? 'border-success bg-success/15 text-success' : 'border-warning bg-warning/15 text-warning') : 'border-border text-muted-foreground'
                )}
                aria-hidden="true"
              >
                {passed && hit ? <Check className="h-4 w-4" /> : <Flag className="h-4 w-4" />}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-baseline justify-between gap-2">
                  <span className="font-medium text-foreground">{formatShortDate(c.date)}</span>
                  <span className="tabular-nums text-foreground">{c.targetKg.toFixed(0)} kg</span>
                </div>
                <div className="flex items-baseline justify-between gap-2 text-xs text-muted-foreground">
                  <span className="truncate">{c.note ?? (passed ? 'Passed' : 'Upcoming')}</span>
                  {delta !== null && <span className={cn('tabular-nums', hit ? 'text-success' : 'text-warning')}>{formatDelta(delta)}</span>}
                </div>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
