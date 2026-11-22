/**
 * Superset group and extra set side for a day's exercises (`PUT /workouts/{id}/exercises`). Exercises
 * with the same superset number are done together in gym mode (2a/2b). The extra set side applies to
 * unilateral reps-per-set exercises only. Saves on its own, separately from weight changes.
 */

import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { useUpdateExercises } from '@/hooks/useWorkouts';
import { canHaveExtraSet, groupingUpdates, type GroupingChoice } from './exerciseGrouping';
import type { ExerciseDto, ExtraSetSide } from '@/types/workout';

const SELECT = 'mt-1 h-12 w-full rounded-md border border-border bg-muted/50 px-3 text-base text-foreground focus:outline-none focus:ring-2 focus:ring-primary';

export function ExerciseGroupingEditor({ workoutId, exercises, onSaved }: { workoutId: string; exercises: ExerciseDto[]; onSaved?: () => void }) {
  const save = useUpdateExercises();
  const [choices, setChoices] = useState<Record<string, GroupingChoice>>({});
  const current = (e: ExerciseDto): GroupingChoice => choices[e.id] ?? { supersetGroup: e.supersetGroup ?? null, extraSetSide: e.extraSetSide ?? 'None' };
  const updates = useMemo(() => groupingUpdates(exercises, choices), [exercises, choices]);
  const set = (e: ExerciseDto, patch: Partial<GroupingChoice>) => setChoices((c) => ({ ...c, [e.id]: { ...current(e), ...patch } }));
  const groups = Array.from({ length: Math.max(3, exercises.length) }, (_, i) => i + 1);

  return (
    <section className="mt-6 rounded-lg border border-border p-4" aria-labelledby="grouping-heading">
      <h3 id="grouping-heading" className="text-lg font-semibold text-foreground">
        Supersets and extra sets
      </h3>
      <p className="mb-3 text-sm text-muted-foreground">Exercises with the same superset number are done back to back (2a, 2b) with one rest after both.</p>
      <ul className="space-y-3">
        {exercises.map((e) => {
          const choice = current(e);
          return (
            <li key={e.id} className="rounded-md border border-border p-3">
              <p className="mb-2 font-medium text-foreground">{e.name}</p>
              <div className="grid gap-3 min-[480px]:grid-cols-2">
                <div>
                  <Label htmlFor={`superset-${e.id}`}>Superset</Label>
                  <select
                    id={`superset-${e.id}`}
                    className={SELECT}
                    value={choice.supersetGroup ?? ''}
                    onChange={(ev) => set(e, { supersetGroup: ev.target.value ? Number(ev.target.value) : null })}
                  >
                    <option value="">None</option>
                    {groups.map((g) => (
                      <option key={g} value={g}>
                        Group {g}
                      </option>
                    ))}
                  </select>
                </div>
                {canHaveExtraSet(e) && (
                  <div>
                    <Label htmlFor={`extra-${e.id}`}>Extra set</Label>
                    <select id={`extra-${e.id}`} className={SELECT} value={choice.extraSetSide} onChange={(ev) => set(e, { extraSetSide: ev.target.value as ExtraSetSide })}>
                      <option value="None">None</option>
                      <option value="Left">Left</option>
                      <option value="Right">Right</option>
                    </select>
                  </div>
                )}
              </div>
            </li>
          );
        })}
      </ul>
      <Button
        type="button"
        className="mt-3 h-12 w-full"
        disabled={updates.length === 0 || save.isPending}
        onClick={() =>
          save.mutate(
            { workoutId, request: { updates } },
            {
              onSuccess: () => {
                setChoices({});
                toast.success('Supersets and extra sets saved');
                onSaved?.();
              },
              onError: () => toast.error('Could not save supersets and extra sets'),
            }
          )
        }
      >
        {save.isPending ? 'Saving…' : 'Save supersets and extra sets'}
      </Button>
    </section>
  );
}
