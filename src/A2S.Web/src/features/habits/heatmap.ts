/** Builds the 12-week heatmap grid for one habit from a history range. */

import { addDays, compareLocalDates, weekStartLocal, type LocalDate } from '@/lib/localDate';
import type { HabitHistoryDto } from '@/types/habits';

export type HeatCell = {
  date: LocalDate;
  state: 'future' | 'none' | 'minimum' | 'target';
  value: number;
};

export const HEATMAP_WEEKS = 12;

/** The Monday that starts the first of the `weeks` weeks ending in the week of `to`. */
export function heatmapStart(to: LocalDate, weeks = HEATMAP_WEEKS): LocalDate {
  return addDays(weekStartLocal(to), -7 * (weeks - 1));
}

/** `weeks` columns of seven cells (Mon…Sun), oldest first. */
export function buildHeatmap(history: HabitHistoryDto | undefined, habitId: string, to: LocalDate, weeks = HEATMAP_WEEKS): HeatCell[][] {
  const byDate = new Map<LocalDate, { value: number; metMinimum: boolean; metTarget: boolean }>();
  for (const entry of history?.entries ?? []) {
    if (entry.habitId === habitId) byDate.set(entry.date, entry);
  }
  const start = heatmapStart(to, weeks);
  const columns: HeatCell[][] = [];
  for (let w = 0; w < weeks; w++) {
    const column: HeatCell[] = [];
    for (let d = 0; d < 7; d++) {
      const date = addDays(start, w * 7 + d);
      if (compareLocalDates(date, to) > 0) {
        column.push({ date, state: 'future', value: 0 });
        continue;
      }
      const entry = byDate.get(date);
      const state = !entry || entry.value <= 0 ? 'none' : entry.metTarget ? 'target' : entry.metMinimum ? 'minimum' : 'none';
      column.push({ date, state, value: entry?.value ?? 0 });
    }
    columns.push(column);
  }
  return columns;
}

export function heatmapSummary(cells: HeatCell[][]): { days: number; minimum: number; target: number } {
  let days = 0;
  let minimum = 0;
  let target = 0;
  for (const column of cells) {
    for (const cell of column) {
      if (cell.state === 'future') continue;
      days++;
      if (cell.state === 'target') {
        target++;
        minimum++;
      } else if (cell.state === 'minimum') {
        minimum++;
      }
    }
  }
  return { days, minimum, target };
}
