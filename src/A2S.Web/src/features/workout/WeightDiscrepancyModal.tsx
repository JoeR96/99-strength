import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { kgToLbs } from "@/utils/constants";
import { getWeekParameters } from "@/utils/weekParameters";
import type { WeightDiscrepancy, ExerciseDto, LinearProgressionDto } from "./workoutSessionTypes";

interface WeightDiscrepancyModalProps {
  discrepancies: WeightDiscrepancy[];
  exerciseUnit: 'Kilograms' | 'Pounds';
  currentWeek: number;
  exercises: ExerciseDto[];
  onApply: (discrepancy: WeightDiscrepancy, confirmedWeight: number, decision: 'skip' | 'update') => Promise<void>;
  onComplete: () => void;
}

export function WeightDiscrepancyModal({ discrepancies, exerciseUnit, currentWeek, exercises, onApply, onComplete }: WeightDiscrepancyModalProps) {
  const [decisions, setDecisions] = useState<Record<string, { weight: number; decision: 'skip' | 'update' | null }>>({});
  const [applying, setApplying] = useState(false);

  const convertWeight = (kgWeight: number) => {
    if (exerciseUnit === 'Pounds') {
      return kgToLbs(kgWeight).toFixed(1);
    }
    return kgWeight.toFixed(1);
  };

  const allDecided = discrepancies.every(disc =>
    decisions[disc.exerciseId]?.decision !== undefined && decisions[disc.exerciseId]?.decision !== null
  );

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const disc of discrepancies) {
        const decision = decisions[disc.exerciseId];
        if (decision?.decision) {
          await onApply(disc, decision.weight, decision.decision);
        }
      }
      onComplete();
    } catch (error) {
      console.error('Failed to apply weight discrepancies:', error);
    } finally {
      setApplying(false);
    }
  };

  return (
    <Dialog open={true} onOpenChange={(open) => { if (!open) onComplete(); }}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="p-4 border-b bg-orange-100 dark:bg-orange-900">
          <div className="flex items-center gap-2 text-orange-800 dark:text-orange-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <DialogTitle>Weight Discrepancies</DialogTitle>
          </div>
          <DialogDescription className="text-sm text-orange-700 dark:text-orange-300 mt-1">
            Hevy weights differ from your program. Choose how to handle each:
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {discrepancies.map((disc) => {
            const exercise = exercises.find(e => e.id === disc.exerciseId);
            const weekParams = getWeekParameters(currentWeek);
            const isLinear = disc.progressionType === 'Linear';
            const linearProg = isLinear && exercise ? (exercise.progression as LinearProgressionDto) : null;
            const expectedWeight = isLinear && linearProg
              ? (linearProg.trainingMax.value * weekParams.intensity).toFixed(1)
              : convertWeight(disc.prescribedWeight);

            return (
              <div key={disc.exerciseId} className="border border-border rounded-lg p-3 bg-zinc-50 dark:bg-zinc-800">
                <div className="mb-3">
                  <div className="font-medium text-base mb-2">{disc.exerciseName}</div>
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <div>
                      <span className="text-zinc-500 dark:text-zinc-400">Expected:</span>
                      <span className="font-medium ml-1">{expectedWeight} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}</span>
                    </div>
                    <div>
                      <span className="text-zinc-500 dark:text-zinc-400">Hevy:</span>
                      <span className="font-bold text-orange-600 dark:text-orange-400 ml-1">
                        {convertWeight(disc.actualWeights[0])} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="flex gap-2">
                  <button
                    onClick={() => setDecisions(prev => ({
                      ...prev,
                      [disc.exerciseId]: { weight: disc.actualWeights[0], decision: 'skip' }
                    }))}
                    aria-pressed={decisions[disc.exerciseId]?.decision === 'skip'}
                    className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                      decisions[disc.exerciseId]?.decision === 'skip'
                        ? 'bg-blue-600 text-white border-blue-600'
                        : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                    }`}
                  >
                    Skip This Week
                  </button>
                  <button
                    onClick={() => setDecisions(prev => ({
                      ...prev,
                      [disc.exerciseId]: { weight: disc.actualWeights[0], decision: 'update' }
                    }))}
                    aria-pressed={decisions[disc.exerciseId]?.decision === 'update'}
                    className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                      decisions[disc.exerciseId]?.decision === 'update'
                        ? 'bg-green-600 text-white border-green-600'
                        : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                    }`}
                  >
                    Update Weight
                  </button>
                </div>
              </div>
            );
          })}
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
