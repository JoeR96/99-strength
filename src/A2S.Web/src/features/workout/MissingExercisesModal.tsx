import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
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
    <Dialog open={true} onOpenChange={(open) => { if (!open) onComplete(); }}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="p-4 border-b bg-blue-100 dark:bg-blue-900/50">
          <div className="flex items-center gap-2 text-blue-800 dark:text-blue-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <DialogTitle>Exercises Not Found in Hevy</DialogTitle>
          </div>
          <DialogDescription className="text-sm text-blue-700 dark:text-blue-300 mt-1">
            These exercises were not completed in Hevy. How would you like to proceed?
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {missingExercises.map((exercise) => (
            <div key={exercise.exerciseId} className="border border-border rounded-lg p-3 bg-zinc-50 dark:bg-zinc-800">
              <div className="mb-3">
                <div className="font-medium text-base mb-2">{exercise.exerciseName}</div>
                <div className="text-sm text-zinc-600 dark:text-zinc-400">
                  Prescribed: {exercise.prescribedSets} sets of {exercise.prescribedReps} @ {convertWeight(exercise.prescribedWeight)} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}
                </div>
                <div className="text-xs text-zinc-500 dark:text-zinc-500 mt-1">
                  This exercise was not found in your Hevy workout
                </div>
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'skip' }))}
                  aria-pressed={decisions[exercise.exerciseId] === 'skip'}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[exercise.exerciseId] === 'skip'
                      ? 'bg-blue-600 text-white border-blue-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Skip This Week
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'delete' }))}
                  aria-pressed={decisions[exercise.exerciseId] === 'delete'}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[exercise.exerciseId] === 'delete'
                      ? 'bg-red-600 text-white border-red-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Remove from Program
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-border bg-zinc-50 dark:bg-zinc-800 flex justify-end gap-2">
          <Button onClick={handleApplyAll} disabled={!allDecided || applying}>
            {applying ? 'Applying...' : 'Apply & Continue'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
