/**
 * The hero of the Current workout page: the next session in the cycle ("Week 1 · Session 2 of 4"),
 * every exercise with a one-line set summary, the cardio finisher as the last item, warm-up count and
 * estimated time, and one big Start. A lift with no training max shows Set TM inline.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Clock, Eye, Flame, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { CycleSessionDto } from '@/api/workouts';
import { SetTrainingMaxDialog } from '@/features/gym/SetTrainingMaxDialog';
import { PlannedCardioItem } from '@/features/gym/PlannedCardioItem';
import type { MobilityRoutineDto } from '@/types/mobility';
import { estimateMinutes, previewHref, setSummary } from './cycle';

export interface CurrentSessionCardProps {
  session: CycleSessionDto;
  daysPerWeek: number;
  warmUp: MobilityRoutineDto | null;
}

export function CurrentSessionCard({ session, daysPerWeek, warmUp }: CurrentSessionCardProps) {
  const [tmFor, setTmFor] = useState<string | null>(null);
  const plan = session.plan;
  const minutes = estimateMinutes(plan, warmUp?.targetMinutes ?? 0);
  const tmExercise = plan.exercises.find((e) => e.exerciseId === tmFor) ?? null;

  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby="current-session-heading" data-testid="current-session">
      <p className="text-sm text-muted-foreground" data-testid="cycle-position">
        Week {session.week} · Session {session.position} of {daysPerWeek}
        {session.isDeloadWeek && <span className="ml-2 rounded-full bg-accent/20 px-2 py-0.5 text-xs font-medium text-foreground">Deload</span>}
      </p>
      <h2 id="current-session-heading" className="text-2xl font-bold text-foreground">
        {session.label}
      </h2>
      <p className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
        <span className="inline-flex items-center gap-1">
          <Clock className="h-4 w-4" aria-hidden="true" />
          About {minutes} min
        </span>
        {warmUp && (
          <span className="inline-flex items-center gap-1" data-testid="warm-up-count">
            <Flame className="h-4 w-4" aria-hidden="true" />
            Warm-up: {warmUp.drills.length} drills
          </span>
        )}
      </p>

      <ul className="mt-3 space-y-2" aria-label="Exercises">
        {plan.exercises.map((exercise) => (
          <li key={exercise.exerciseId} className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 text-sm">
            <span className="min-w-0 truncate font-medium text-foreground">{exercise.name}</span>
            {exercise.metadata.isTrainingMaxPending ? (
              <Button
                type="button"
                variant="outline"
                className="h-11"
                onClick={() => setTmFor(exercise.exerciseId)}
                aria-label={`Set TM for ${exercise.name}`}
                data-testid="set-tm"
              >
                Set TM
              </Button>
            ) : (
              <span className="shrink-0 text-muted-foreground">{setSummary(exercise)}</span>
            )}
          </li>
        ))}
        {plan.cardio && <PlannedCardioItem cardio={plan.cardio} compact />}
      </ul>
      {plan.cardio?.stepUpNote && <p className="mt-1 text-xs text-muted-foreground">{plan.cardio.progressionType} · {plan.cardio.stepUpNote}</p>}

      <div className="mt-4 grid grid-cols-[1fr_auto] gap-2">
        <Button asChild size="lg" className="h-14 text-lg" data-testid="start-workout">
          <Link to={`/gym/${session.day}`}>
            <Play aria-hidden="true" />
            Start workout
          </Link>
        </Button>
        <Button asChild variant="outline" size="lg" className="h-14" aria-label={`Preview ${session.label}`}>
          <Link to={previewHref(session)}>
            <Eye aria-hidden="true" />
          </Link>
        </Button>
      </div>

      {tmExercise && (
        <SetTrainingMaxDialog
          open
          workoutId={plan.workoutId}
          exerciseId={tmExercise.exerciseId}
          exerciseName={tmExercise.name}
          onClose={() => setTmFor(null)}
          onSaved={(result) => toast.success(`Training max set: ${result.trainingMax} kg`)}
        />
      )}
    </section>
  );
}
