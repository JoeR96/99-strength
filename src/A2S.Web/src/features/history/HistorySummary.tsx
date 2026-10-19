import { Card } from '@/components/ui/card';
import { oneRepMaxSeries, programTotals, unitLabel } from './historyStats';
import type { WorkoutHistoryDto } from './historyTypes';

const shortName = (name: string) => name.replace(/\s*\(.*\)$/, '');

/** Four headline numbers for the program so far. */
export function HistorySummary({ history }: { history: WorkoutHistoryDto }) {
  const totals = programTotals(history);
  const done = Math.min(100, Math.round((totals.sessions / Math.max(1, totals.planned)) * 100));

  // The strongest lift: the highest estimated one-rep max any week has produced
  let strongest: { name: string; estimate: number; unit: string } | null = null;
  for (const exercise of history.exerciseHistories) {
    for (const point of oneRepMaxSeries(exercise)) {
      if (!strongest || point.estimate > strongest.estimate) {
        strongest = { name: shortName(exercise.name), estimate: point.estimate, unit: unitLabel(exercise.weightUnit) };
      }
    }
  }

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <Card className="p-4">
        <p className="text-sm text-muted-foreground">Sessions</p>
        <p className="mt-1 text-2xl font-semibold text-foreground">
          {totals.sessions}
          <span className="ml-1 text-sm font-normal text-muted-foreground">of {totals.planned}</span>
        </p>
        <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden="true">
          <div className="h-full rounded-full bg-primary" style={{ width: `${done}%` }} />
        </div>
      </Card>
      <Card className="p-4">
        <p className="text-sm text-muted-foreground">Volume lifted</p>
        <p className="mt-1 text-2xl font-semibold text-foreground">
          {Math.round(totals.volume).toLocaleString()}
          <span className="ml-1 text-sm font-normal text-muted-foreground">kg</span>
        </p>
        <p className="text-caption mt-2">weight × reps, every set</p>
      </Card>
      <Card className="p-4">
        <p className="text-sm text-muted-foreground">Sets logged</p>
        <p className="mt-1 text-2xl font-semibold text-foreground">{totals.sets.toLocaleString()}</p>
        <p className="text-caption mt-2">
          {totals.sessions > 0 ? Math.round(totals.sets / totals.sessions) : 0} a session
        </p>
      </Card>
      <Card className="p-4">
        <p className="text-sm text-muted-foreground">Best estimated 1RM</p>
        <p className="mt-1 text-2xl font-semibold text-foreground">
          {strongest ? strongest.estimate.toFixed(1) : '–'}
          {strongest && <span className="ml-1 text-sm font-normal text-muted-foreground">{strongest.unit}</span>}
        </p>
        <p className="text-caption mt-2">{strongest ? strongest.name : 'No AMRAP sets yet'}</p>
      </Card>
    </div>
  );
}
