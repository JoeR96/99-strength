import { useMemo, useState } from 'react';
import { useAddExercise, useCurrentWorkout } from '@/hooks/useWorkouts';
import { ExerciseCategory, WeightUnit, type DayNumber } from '@/types/workout';

interface AddToPlanPanelProps {
  /** Library / Hevy title, e.g. "Bicep Curl (Dumbbell)". */
  templateName: string;
  /** Hevy exercise template id, so the plan posts it to Hevy as the same exercise. */
  hevyTemplateId: string;
}

const field = 'w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm';

/**
 * Adds the exercise to the running plan as a reps-per-set accessory. It then shows up every week on that
 * day and progresses like the other accessories (all sets at the top of the range → weight goes up).
 */
export function AddToPlanPanel({ templateName, hevyTemplateId }: AddToPlanPanelProps) {
  const { data: workout } = useCurrentWorkout();
  const addExercise = useAddExercise();

  const days = useMemo(() => {
    const byDay = new Map<number, { name: string; orderInDay: number }[]>();
    for (const e of workout?.exercises ?? []) {
      byDay.set(e.assignedDay, [...(byDay.get(e.assignedDay) ?? []), { name: e.name, orderInDay: e.orderInDay }]);
    }
    return [...byDay.entries()]
      .sort(([a], [b]) => a - b)
      .map(([day, list]) => ({ day, exercises: list.sort((a, b) => a.orderInDay - b.orderInDay) }));
  }, [workout]);

  const [day, setDay] = useState<number | null>(null);
  const [position, setPosition] = useState<number | null>(null);
  const [sets, setSets] = useState(2);
  const [repMin, setRepMin] = useState(10);
  const [repMax, setRepMax] = useState(12);
  const [weight, setWeight] = useState('');

  if (!workout) {
    return null;
  }

  const chosenDay = day ?? days[0]?.day ?? 1;
  const dayExercises = days.find((d) => d.day === chosenDay)?.exercises ?? [];
  const lastSlot = Math.max(0, ...dayExercises.map((e) => e.orderInDay)) + 1;
  const slot = position ?? lastSlot;
  const alreadyThere = dayExercises.some((e) => e.name.toLowerCase() === templateName.toLowerCase());
  const weightKg = weight.trim() === '' ? undefined : Number(weight);
  const invalid = repMin > repMax || sets < 1 || (weightKg !== undefined && (Number.isNaN(weightKg) || weightKg < 0));

  const submit = () =>
    addExercise.mutate({
      workoutId: workout.id,
      request: {
        templateName,
        externalTemplateId: hevyTemplateId,
        category: ExerciseCategory.Accessory,
        progressionType: 'RepsPerSet',
        assignedDay: chosenDay as DayNumber,
        orderInDay: slot,
        startingSets: sets,
        targetSets: sets + 2,
        repRangeMinimum: repMin,
        repRangeMaximum: repMax,
        ...(weightKg !== undefined ? { startingWeight: weightKg, weightUnit: WeightUnit.Kilograms } : {}),
      },
    });

  return (
    <section aria-label="Add to plan" className="mb-6 rounded-lg border border-border bg-muted/20 p-4">
      <h3 className="mb-3 text-sm font-semibold text-foreground">Add to {workout.name}</h3>

      {addExercise.isSuccess ? (
        <p role="status" className="text-sm text-foreground">
          Added to Day {addExercise.data.assignedDay} as exercise {addExercise.data.orderInDay}. It's in the plan every week from now on.
        </p>
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <label className="text-xs text-muted-foreground">
            Day
            <select
              className={field}
              value={chosenDay}
              onChange={(e) => {
                setDay(Number(e.target.value));
                setPosition(null);
              }}
            >
              {days.map((d) => (
                <option key={d.day} value={d.day}>
                  Day {d.day}
                  {d.exercises[0] ? ` · ${d.exercises[0].name}` : ''}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs text-muted-foreground col-span-1 sm:col-span-2">
            Position
            <select className={field} value={slot} onChange={(e) => setPosition(Number(e.target.value))}>
              {dayExercises.map((e) => (
                <option key={e.orderInDay} value={e.orderInDay}>
                  Before {e.name}
                </option>
              ))}
              <option value={lastSlot}>Last</option>
            </select>
          </label>

          <label className="text-xs text-muted-foreground">
            Sets
            <input className={field} type="number" min={1} max={10} value={sets} onChange={(e) => setSets(Number(e.target.value))} />
          </label>
          <label className="text-xs text-muted-foreground">
            Reps
            <span className="flex items-center gap-1">
              <input aria-label="Minimum reps" className={field} type="number" min={1} value={repMin} onChange={(e) => setRepMin(Number(e.target.value))} />
              –
              <input aria-label="Maximum reps" className={field} type="number" min={1} value={repMax} onChange={(e) => setRepMax(Number(e.target.value))} />
            </span>
          </label>
          <label className="text-xs text-muted-foreground">
            Weight (kg)
            <input className={field} inputMode="decimal" placeholder="Confirm later" value={weight} onChange={(e) => setWeight(e.target.value)} />
          </label>

          <div className="col-span-2 sm:col-span-3 flex items-center gap-3">
            <button
              type="button"
              onClick={submit}
              disabled={invalid || addExercise.isPending}
              className="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground disabled:opacity-50"
            >
              {addExercise.isPending ? 'Adding…' : 'Add to plan'}
            </button>
            {alreadyThere && <span className="text-xs text-muted-foreground">Already on this day.</span>}
            {addExercise.isError && (
              <span role="alert" className="text-xs text-destructive">
                {addExercise.error instanceof Error ? addExercise.error.message : 'Could not add it.'}
              </span>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
