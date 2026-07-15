import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import type { PendingWeightExerciseDto } from "@/types/workout";

export type WeightConfirmationPhase = "pre-completion" | "post-completion";

interface WeightConfirmationModalProps {
  exercises: PendingWeightExerciseDto[];
  /**
   * pre-completion: starting weights must be confirmed before the day is submitted.
   * Dismissing cancels the completion entirely.
   * post-completion: progression raised Cable/Machine weights; confirming aligns the
   * suggested weight to the gym's actual stack. Dismissing skips (re-asked next time).
   */
  phase: WeightConfirmationPhase;
  onConfirm: (confirmedWeights: { exerciseId: string; weight: number; unit: 1 | 2 }[]) => Promise<void>;
  onSkip: () => void;
}

export function WeightConfirmationModal({ exercises, phase, onConfirm, onSkip }: WeightConfirmationModalProps) {
  const [weights, setWeights] = useState<Record<string, number>>(() => {
    const initial: Record<string, number> = {};
    for (const ex of exercises) {
      initial[ex.exerciseId] = ex.suggestedWeight;
    }
    return initial;
  });
  const [confirming, setConfirming] = useState(false);

  const isPre = phase === "pre-completion";
  const title = isPre ? "Confirm Starting Weights" : "Confirm New Working Weights";
  const description = isPre
    ? "These exercises were completed for the first time. Confirm the starting weight to finish this workout:"
    : "These exercises progressed to a heavier weight. Adjust to match your gym's weight stack:";

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
    <Dialog open={true} onOpenChange={(open) => { if (!open) onSkip(); }}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="p-4 border-b bg-blue-100 dark:bg-blue-900">
          <div className="flex items-center gap-2 text-blue-800 dark:text-blue-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
            </svg>
            <DialogTitle>{title}</DialogTitle>
          </div>
          <DialogDescription className="text-sm text-blue-700 dark:text-blue-300 mt-1">
            {description}
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4">
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
        </div>

        <div className="p-4 border-t flex gap-2 justify-end">
          <Button variant="outline" onClick={onSkip} disabled={confirming}>
            {isPre ? "Cancel" : "Skip for now"}
          </Button>
          <Button onClick={handleConfirm} disabled={confirming}>
            {confirming
              ? "Confirming..."
              : isPre
                ? "Confirm & Complete Workout"
                : "Confirm Weights"}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
