/**
 * The hero of the Current workout page: the next session in the cycle ("Week 1 · Session 2 of 4"),
 * every exercise with a one-line set summary, the cardio finisher as the last item, warm-up count and
 * estimated time, and one big Start. A lift with no training max shows Set TM inline, with a Hevy-based
 * suggestion when there is one, and "Use all suggestions" sets every pending lift at once after review.
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
import { useSetTrainingMax, useStartingNumberSuggestions } from '@/hooks/useWorkouts';
import { itemFor, suggestionLine } from '@/features/suggestions/startingNumbers';
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
  const pending = plan.exercises.filter((e) => e.metadata.isTrainingMaxPending);
  const suggestions = useStartingNumberSuggestions(plan.workoutId, pending.length > 0);
  const suggested = pending
    .map((e) => ({ exercise: e, suggestion: itemFor(suggestions.data, e.exerciseId)?.suggestion ?? null }))
    .filter((p): p is { exercise: (typeof pending)[number]; suggestion: NonNullable<typeof p.suggestion> } => p.suggestion !== null);
  const [reviewing, setReviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const setTm = useSetTrainingMax();

  const applyAll = async () => {
    setApplying(true);
    try {
      for (const { exercise, suggestion } of suggested) {
        await setTm.mutateAsync({ workoutId: plan.workoutId, exerciseId: exercise.exerciseId, request: { trainingMax: suggestion.valueKg, unit: 'Kilograms' } });
      }
      toast.success(`Training max set for ${suggested.length} ${suggested.length === 1 ? 'lift' : 'lifts'}`);
      setReviewing(false);
    } catch {
      toast.error('Could not set every training max. Try again.');
    } finally {
      setApplying(false);
    }
  };

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
            {exercise.metadata.isTrainingMaxPending && itemFor(suggestions.data, exercise.exerciseId)?.suggestion && (
              <span className="w-full text-xs text-muted-foreground" data-testid="tm-suggestion">
                {suggestionLine(itemFor(suggestions.data, exercise.exerciseId)!.suggestion!)}
              </span>
            )}
          </li>
        ))}
        {plan.cardio && <PlannedCardioItem cardio={plan.cardio} compact />}
      </ul>
      {suggested.length > 0 && !reviewing && (
        <Button type="button" variant="outline" className="mt-3 h-11 w-full" onClick={() => setReviewing(true)} data-testid="use-all-suggestions">
          Use all suggestions
        </Button>
      )}
      {reviewing && (
        <div className="mt-3 space-y-2 rounded-md border border-border bg-muted/40 p-3" role="group" aria-label="Review suggested training maxes" data-testid="suggestions-review">
          <p className="text-sm font-medium text-foreground">Set these training maxes?</p>
          <ul className="space-y-1 text-sm">
            {suggested.map(({ exercise, suggestion }) => (
              <li key={exercise.exerciseId} className="flex justify-between gap-3">
                <span className="min-w-0 truncate text-foreground">{exercise.name}</span>
                <span className="shrink-0 text-muted-foreground">
                  {suggestion.valueKg} kg{suggestion.stale ? ' (older history)' : ''}
                </span>
              </li>
            ))}
          </ul>
          <p className="text-xs text-muted-foreground">Disagree with one? Cancel and use Set TM for that lift.</p>
          <div className="grid grid-cols-2 gap-2">
            <Button type="button" variant="outline" className="h-11" onClick={() => setReviewing(false)} disabled={applying}>
              Cancel
            </Button>
            <Button type="button" className="h-11" onClick={applyAll} disabled={applying} data-testid="confirm-all-suggestions">
              {applying ? 'Setting…' : `Set ${suggested.length}`}
            </Button>
          </div>
        </div>
      )}
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
