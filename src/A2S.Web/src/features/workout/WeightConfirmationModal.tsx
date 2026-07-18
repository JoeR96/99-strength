import { useState } from "react";
import { ReviewModal } from "@/components/shared/ReviewModal";
import { Input } from "@/components/ui/input";
import type { PendingWeightExerciseDto } from "@/types/workout";

interface WeightConfirmationModalProps {
  exercises: PendingWeightExerciseDto[];
  /**
   * Starting weights must be confirmed before the day is submitted.
   * Dismissing cancels the completion entirely. (Working-weight bumps from
   * progression are never prompted — they're confirmed implicitly by the
   * weight logged at the next session.)
   */
  onConfirm: (confirmedWeights: { exerciseId: string; weight: number; unit: 1 | 2 }[]) => Promise<void>;
  onSkip: () => void;
}

export function WeightConfirmationModal({ exercises, onConfirm, onSkip }: WeightConfirmationModalProps) {
  const [weights, setWeights] = useState<Record<string, number>>(() => {
    const initial: Record<string, number> = {};
    for (const ex of exercises) {
      initial[ex.exerciseId] = ex.suggestedWeight;
    }
    return initial;
  });
  const [confirming, setConfirming] = useState(false);

  const handleConfirm = async () => {
    setConfirming(true);
    try {
      const confirmed = exercises.map((ex) => ({
        exerciseId: ex.exerciseId,
        weight: weights[ex.exerciseId] ?? ex.suggestedWeight,
        unit: (ex.weightUnit === "Pounds" ? 2 : 1) as 1 | 2,
      }));
      await onConfirm(confirmed);
    } finally {
      setConfirming(false);
    }
  };

  return (
    <ReviewModal
      open={true}
      onOpenChange={(open) => { if (!open) onSkip(); }}
      title="Confirm Starting Weights"
      description="These exercises were completed for the first time. Confirm the starting weight to finish this workout:"
      headerClassName="bg-blue-100 text-blue-800"
      icon={
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
        </svg>
      }
      actions={[
        {
          label: "Cancel",
          onClick: onSkip,
          variant: "outline",
          disabled: confirming,
        },
        {
          label: confirming ? "Confirming..." : "Confirm & Complete Workout",
          onClick: handleConfirm,
          disabled: confirming,
        },
      ]}
    >
      {exercises.map((ex) => {
        const unitLabel = ex.weightUnit === "Pounds" ? "lbs" : "kg";
        return (
          <div key={ex.exerciseId} className="flex items-center justify-between gap-4 p-3 rounded-lg border bg-card">
            <div className="flex-1 min-w-0">
              <p className="font-medium truncate">{ex.exerciseName}</p>
              <p className="text-xs text-muted-foreground">
                Suggested: {ex.suggestedWeight} {unitLabel}
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <Input
                type="number"
                min={0}
                step={0.5}
                value={weights[ex.exerciseId] ?? ""}
                onChange={(e) =>
                  setWeights((prev) => ({
                    ...prev,
                    [ex.exerciseId]: parseFloat(e.target.value) || 0,
                  }))
                }
                className="w-24 text-right"
              />
              <span className="text-sm text-muted-foreground w-6">{unitLabel}</span>
            </div>
          </div>
        );
      })}
    </ReviewModal>
  );
}
