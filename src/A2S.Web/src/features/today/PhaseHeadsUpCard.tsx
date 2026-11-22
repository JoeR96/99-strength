/**
 * Phase heads-up on Today: the week before a phase starts and on its first day, what changes
 * ("Back squat replaces pause squat", "Stair finisher: start intervals"). Exercise swaps are a
 * suggestion: tapping one asks to confirm, then runs the existing permanent SubstituteExercise.
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { plansApi } from '@/api/plans';
import { workoutsApi } from '@/api/workouts';
import { formatDayLabel, type LocalDate } from '@/lib/localDate';
import { phaseTypeLabel, type PhaseHeadsUpDto, type PhaseSwapActionDto } from '@/types/plan';

export interface PhaseHeadsUpCardProps {
  headsUp: PhaseHeadsUpDto;
  onSwap: (swap: PhaseSwapActionDto) => void;
  swapping?: boolean;
}

function whenText(headsUp: PhaseHeadsUpDto): string {
  if (headsUp.daysUntil <= 0) return 'starts today';
  if (headsUp.daysUntil === 1) return 'starts tomorrow';
  return `starts ${formatDayLabel(headsUp.from)} (in ${headsUp.daysUntil} days)`;
}

export function PhaseHeadsUpCard({ headsUp, onSwap, swapping = false }: PhaseHeadsUpCardProps) {
  const [confirming, setConfirming] = useState<PhaseSwapActionDto | null>(null);
  const name = headsUp.label ?? phaseTypeLabel[headsUp.type];

  return (
    <section aria-labelledby="phase-heads-up-heading" className="space-y-3 rounded-lg border border-primary/50 bg-card p-4" data-testid="phase-heads-up">
      <h2 id="phase-heads-up-heading" className="text-base font-semibold text-foreground">
        {name} {whenText(headsUp)}
      </h2>
      {headsUp.changes.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-sm text-foreground">
          {headsUp.changes.map((c) => (
            <li key={c}>{c}</li>
          ))}
        </ul>
      )}
      {headsUp.swaps.map((swap) => {
        const key = `${swap.fromExercise}->${swap.toExercise}`;
        if (swap.done) {
          return (
            <p key={key} className="text-sm text-muted-foreground">
              {swap.toExercise} is already in your workout.
            </p>
          );
        }
        if (!swap.exerciseId || !swap.workoutId) {
          return (
            <p key={key} className="text-sm text-muted-foreground">
              Swap {swap.fromExercise} for {swap.toExercise} (not in your linked workout).
            </p>
          );
        }
        if (confirming === swap) {
          return (
            <div key={key} className="space-y-2 rounded-md border border-border p-3" role="group" aria-label="Confirm swap">
              <p className="text-sm text-foreground">
                Replace {swap.fromExercise} with {swap.toExercise} for good? Its progression carries on.
              </p>
              <div className="grid grid-cols-2 gap-2">
                <Button className="h-11" disabled={swapping} onClick={() => onSwap(swap)}>
                  Swap
                </Button>
                <Button variant="outline" className="h-11" onClick={() => setConfirming(null)}>
                  Cancel
                </Button>
              </div>
            </div>
          );
        }
        return (
          <Button key={key} variant="outline" className="h-auto min-h-11 w-full whitespace-normal" onClick={() => setConfirming(swap)}>
            Swap {swap.fromExercise} → {swap.toExercise}
          </Button>
        );
      })}
    </section>
  );
}

const headsUpKey = (date: LocalDate) => ['plans', 'phase-heads-up', date] as const;

/** Loads the heads-up for today; renders nothing outside a heads-up window, while loading or on error. */
export function TodayPhaseHeadsUp({ date }: { date: LocalDate }) {
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: headsUpKey(date), queryFn: () => plansApi.getPhaseHeadsUp(date) });
  const swap = useMutation({
    mutationFn: (s: PhaseSwapActionDto) =>
      workoutsApi.substituteExercise(s.workoutId!, {
        exerciseId: s.exerciseId!,
        newExerciseName: s.toExercise,
        reason: `Phase start: ${query.data?.label ?? 'new phase'}`,
      }),
    onSuccess: (_result, s) => {
      toast.success(`${s.toExercise} replaces ${s.fromExercise}`);
      return queryClient.invalidateQueries({ queryKey: headsUpKey(date) });
    },
    onError: () => toast.error("Couldn't swap that exercise. Try again."),
  });

  if (!query.data) return null;
  return <PhaseHeadsUpCard headsUp={query.data} onSwap={(s) => swap.mutate(s)} swapping={swap.isPending} />;
}
