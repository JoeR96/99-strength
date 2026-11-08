/** Twelve weeks of one habit: grey none, amber met the minimum, green met the target. */

import { useMemo } from 'react';
import { cn } from '@/lib/utils';
import { formatDayLabel, type LocalDate } from '@/lib/localDate';
import type { HabitDto, HabitHistoryDto } from '@/types/habits';
import { buildHeatmap, heatmapSummary, type HeatCell } from './heatmap';

export interface HabitHeatmapProps {
  habit: HabitDto;
  history: HabitHistoryDto | undefined;
  to: LocalDate;
}

const cellClass: Record<HeatCell['state'], string> = {
  future: 'bg-transparent',
  none: 'bg-muted',
  minimum: 'bg-warning/70',
  target: 'bg-success',
};

const stateLabel: Record<HeatCell['state'], string> = {
  future: 'not yet',
  none: 'missed',
  minimum: 'minimum met',
  target: 'target met',
};

export function HabitHeatmap({ habit, history, to }: HabitHeatmapProps) {
  const cells = useMemo(() => buildHeatmap(history, habit.id, to), [history, habit.id, to]);
  const summary = heatmapSummary(cells);

  return (
    <section aria-label={`${habit.name}, last 12 weeks`} className="rounded-lg border border-border bg-card p-4" data-testid="habit-heatmap">
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-medium text-foreground">{habit.name}</h3>
        <p className="text-xs text-muted-foreground">
          Target {summary.target} · minimum {summary.minimum} of {summary.days} days
        </p>
      </div>
      <div className="flex gap-1" role="img" aria-label={`${summary.target} days met the target and ${summary.minimum} the minimum, of ${summary.days}`}>
        <div className="flex flex-col gap-1 text-[10px] leading-none text-muted-foreground" aria-hidden="true">
          {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((d, i) => (
            <span key={i} className="flex h-[18px] w-3 items-center">
              {d}
            </span>
          ))}
        </div>
        <div className="grid flex-1 grid-flow-col gap-1" style={{ gridTemplateRows: 'repeat(7, 18px)' }}>
          {cells.flat().map((cell) => (
            <span
              key={cell.date}
              className={cn('h-[18px] rounded-sm', cellClass[cell.state])}
              title={`${formatDayLabel(cell.date)}: ${stateLabel[cell.state]}${cell.value > 0 ? ` (${cell.value})` : ''}`}
            />
          ))}
        </div>
      </div>
      <div className="mt-3 flex items-center gap-3 text-[11px] text-muted-foreground">
        <span className="flex items-center gap-1">
          <span className="h-3 w-3 rounded-sm bg-success" aria-hidden="true" /> target
        </span>
        <span className="flex items-center gap-1">
          <span className="h-3 w-3 rounded-sm bg-warning/70" aria-hidden="true" /> minimum
        </span>
        <span className="flex items-center gap-1">
          <span className="h-3 w-3 rounded-sm bg-muted" aria-hidden="true" /> missed
        </span>
      </div>
    </section>
  );
}
