import { useState } from "react";
import { ReviewModal } from "@/components/shared/ReviewModal";
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
    <ReviewModal
      open={true}
      onOpenChange={(open) => { if (!open) onComplete(); }}
      title="Weight Discrepancies"
      description="Hevy weights differ from your program. Choose how to handle each:"
      headerClassName="bg-orange-100 text-orange-800"
      icon={
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
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
      {discrepancies.map((disc) => {
        const exercise = exercises.find(e => e.id === disc.exerciseId);
        const weekParams = getWeekParameters(currentWeek);
        const isLinear = disc.progressionType === 'Linear';
        const linearProg = isLinear && exercise ? (exercise.progression as LinearProgressionDto) : null;
        const expectedWeight = isLinear && linearProg
          ? (linearProg.trainingMax.value * weekParams.intensity).toFixed(1)
          : convertWeight(disc.prescribedWeight);

        return (
          <div key={disc.exerciseId} className="border border-border rounded-lg p-3 bg-zinc-50">
            <div className="mb-3">
              <div className="font-medium text-base mb-2">{disc.exerciseName}</div>
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-zinc-500">Expected:</span>
                  <span className="font-medium ml-1">{expectedWeight} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}</span>
                </div>
                <div>
                  <span className="text-zinc-500">Hevy:</span>
                  <span className="font-bold text-orange-600 ml-1">
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
                    : 'bg-white hover:bg-zinc-100 border-zinc-300 text-zinc-700'
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
                    : 'bg-white hover:bg-zinc-100 border-zinc-300 text-zinc-700'
                }`}
              >
                Update Weight
              </button>
            </div>
          </div>
        );
      })}
    </ReviewModal>
  );
}
