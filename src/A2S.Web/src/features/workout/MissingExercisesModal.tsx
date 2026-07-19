import { useState } from "react";
import { ReviewModal } from "@/components/shared/ReviewModal";
import { kgToLbs } from "@/utils/constants";
import type { MissingExercise } from "./workoutSessionTypes";

interface MissingExercisesModalProps {
  missingExercises: MissingExercise[];
  exerciseUnit: 'Kilograms' | 'Pounds';
  onApply: (exercise: MissingExercise, decision: 'delete' | 'skip') => Promise<void>;
  onComplete: () => void;
}

export function MissingExercisesModal({ missingExercises, exerciseUnit, onApply, onComplete }: MissingExercisesModalProps) {
  const [decisions, setDecisions] = useState<Record<string, 'delete' | 'skip' | null>>({});
  const [applying, setApplying] = useState(false);

  const convertWeight = (kgWeight: number) => {
    if (exerciseUnit === 'Pounds') {
      return kgToLbs(kgWeight).toFixed(1);
    }
    return kgWeight.toFixed(1);
  };

  const allDecided = missingExercises.every(ex => decisions[ex.exerciseId] !== undefined && decisions[ex.exerciseId] !== null);

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const exercise of missingExercises) {
        const decision = decisions[exercise.exerciseId];
        if (decision) {
          await onApply(exercise, decision);
        }
      }
      onComplete();
    } catch (error) {
      console.error('Failed to apply missing exercise decisions:', error);
    } finally {
      setApplying(false);
    }
  };

  return (
    <ReviewModal
      open={true}
      onOpenChange={(open) => { if (!open) onComplete(); }}
      title="Exercises Not Found in Hevy"
      description="These exercises were not completed in Hevy. How would you like to proceed?"
      headerClassName="bg-neon-blue/15 text-neon-blue"
      icon={
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      }
      actions={[
        {
          label: applying ? 'Applying...' : 'Apply & Continue',
          onClick: handleApplyAll,
          disabled: !allDecided || applying,
        },
      ]}
    >
      {missingExercises.map((exercise) => (
        <div key={exercise.exerciseId} className="border border-border rounded-lg p-3 bg-muted/30">
          <div className="mb-3">
            <div className="font-medium text-base mb-2">{exercise.exerciseName}</div>
            <div className="text-sm text-muted-foreground">
              Prescribed: {exercise.prescribedSets} sets of {exercise.prescribedReps} @ {convertWeight(exercise.prescribedWeight)} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}
            </div>
            <div className="text-xs text-muted-foreground mt-1">
              This exercise was not found in your Hevy workout
            </div>
          </div>

          <div className="flex gap-2">
            <button
              onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'skip' }))}
              aria-pressed={decisions[exercise.exerciseId] === 'skip'}
              className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                decisions[exercise.exerciseId] === 'skip'
                  ? 'bg-neon-blue text-white border-neon-blue'
                  : 'bg-card hover:bg-muted border-border text-foreground'
              }`}
            >
              Skip This Week
            </button>
            <button
              onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'delete' }))}
              aria-pressed={decisions[exercise.exerciseId] === 'delete'}
              className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                decisions[exercise.exerciseId] === 'delete'
                  ? 'bg-destructive text-white border-destructive'
                  : 'bg-card hover:bg-muted border-border text-foreground'
              }`}
            >
              Remove from Program
            </button>
          </div>
        </div>
      ))}
    </ReviewModal>
  );
}
