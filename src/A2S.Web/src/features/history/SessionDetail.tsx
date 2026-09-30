import { Card } from '@/components/ui/card';
import { getBlockColor } from '@/lib/blockColors';
import { estimateOneRepMax, sessionTotals, unitLabel } from './historyStats';
import type { ExerciseHistoryDto, WorkoutActivityDto } from './historyTypes';

const CHIP = 'inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-foreground';

/** One session's sets, beside the calendar. */
export function SessionDetail({
  activity,
  date,
  exercises,
}: {
  activity: WorkoutActivityDto;
  date: Date;
  exercises: ExerciseHistoryDto[];
}) {
  const totals = sessionTotals(activity);
  const nameOf = (exerciseId: string) =>
    exercises.find((e) => e.exerciseId === exerciseId)?.name ?? 'Unknown exercise';

  return (
    <Card className="space-y-4 p-6">
      <div>
        <h2 className="text-xl font-semibold text-foreground">
          {date.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}
        </h2>
        <div className="mt-2 flex flex-wrap gap-2">
          <span className={CHIP}>
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: getBlockColor(activity.blockNumber) }} />
            Block {activity.blockNumber}
          </span>
          <span className={CHIP}>
            Week {activity.weekNumber} · Day {activity.dayNumber}
          </span>
          {activity.isDeloadWeek && <span className={CHIP}>Deload</span>}
        </div>
      </div>

      <dl className="grid grid-cols-3 gap-3">
        {[
          ['Exercises', totals.exercises.toString()],
          ['Sets', totals.sets.toString()],
          ['Volume', `${Math.round(totals.volume).toLocaleString()} kg`],
        ].map(([label, value]) => (
          <div key={label} className="rounded-md bg-muted/30 p-3 text-center">
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className="text-lg font-semibold text-foreground">{value}</dd>
          </div>
        ))}
      </dl>

      <ul className="space-y-3">
        {activity.performances.map((performance) => {
          const amrap = performance.completedSets.find((s) => s.wasAmrap);
          return (
            <li key={performance.exerciseId} className="border-b border-border/50 pb-3 last:border-0 last:pb-0">
              <div className="mb-2 flex items-baseline justify-between gap-3">
                <h3 className="text-sm font-medium text-foreground">{nameOf(performance.exerciseId)}</h3>
                {amrap && (
                  <span className="shrink-0 text-xs text-muted-foreground">
                    est. 1RM{' '}
                    <span className="font-mono text-foreground">
                      {estimateOneRepMax(amrap.weight, amrap.actualReps).toFixed(1)} {unitLabel(amrap.weightUnit)}
                    </span>
                  </span>
                )}
              </div>
              <div className="flex flex-wrap gap-1.5">
                {performance.completedSets.map((set) => (
                  <span
                    key={set.setNumber}
                    className={
                      set.wasAmrap
                        ? 'rounded-md border border-primary/30 bg-primary/10 px-2 py-1 text-xs text-primary'
                        : 'rounded-md bg-muted/50 px-2 py-1 text-xs text-foreground'
                    }
                  >
                    <span className="font-mono">
                      {set.weight} {unitLabel(set.weightUnit)} × {set.actualReps}
                    </span>
                    {set.wasAmrap && <span className="ml-1">AMRAP</span>}
                  </span>
                ))}
              </div>
            </li>
          );
        })}
      </ul>
    </Card>
  );
}
