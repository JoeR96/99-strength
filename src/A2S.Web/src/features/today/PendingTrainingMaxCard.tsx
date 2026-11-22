/**
 * Today: A2S2 lifts that were started without a training max. Each needs one before its first
 * session; "Set your training max" opens the same dialog as gym mode. Shows nothing otherwise.
 */

import { useState } from 'react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { SetTrainingMaxDialog } from '@/features/gym/SetTrainingMaxDialog';

export function PendingTrainingMaxCard() {
  const { data: workout } = useCurrentWorkout();
  const [open, setOpen] = useState<string | null>(null);
  const pending = (workout?.exercises ?? []).filter((e) => e.isTrainingMaxPending);
  if (!workout || pending.length === 0) return null;
  const current = pending.find((e) => e.id === open) ?? null;

  return (
    <section className="rounded-lg border border-warning/50 bg-warning/10 p-4" aria-labelledby="pending-tm-heading" data-testid="pending-tm-card">
      <h2 id="pending-tm-heading" className="text-lg font-semibold text-foreground">
        Set your training max
      </h2>
      <p className="mt-1 text-sm text-foreground">
        {pending.length === 1 ? 'This lift needs' : 'These lifts need'} a training max before {pending.length === 1 ? 'its' : 'their'} first session. Until then
        their sets show reps and % only.
      </p>
      <ul className="mt-3 space-y-2">
        {pending.map((e) => (
          <li key={e.id} className="flex flex-wrap items-center gap-2">
            <span className="min-w-0 flex-1 font-medium text-foreground">{e.name}</span>
            <Button type="button" variant="outline" className="h-11" onClick={() => setOpen(e.id)} aria-label={`Set training max for ${e.name}`}>
              Set TM
            </Button>
          </li>
        ))}
      </ul>
      {current && (
        <SetTrainingMaxDialog
          open
          workoutId={workout.id}
          exerciseId={current.id}
          exerciseName={current.name}
          onClose={() => setOpen(null)}
          onSaved={(result) => toast.success(`Training max set: ${result.trainingMax} kg`)}
        />
      )}
    </section>
  );
}
