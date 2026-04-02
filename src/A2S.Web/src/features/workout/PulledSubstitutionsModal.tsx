import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import type { DetectedSubstitution } from "./workoutSessionTypes";

interface PulledSubstitutionsModalProps {
  substitutions: DetectedSubstitution[];
  onApply: (sub: DetectedSubstitution, isPermanent: boolean) => Promise<void>;
  onRemove: (sub: DetectedSubstitution) => Promise<void>;
  onComplete: () => void;
}

export function PulledSubstitutionsModal({ substitutions, onApply, onRemove, onComplete }: PulledSubstitutionsModalProps) {
  const [decisions, setDecisions] = useState<Record<string, 'temporary' | 'permanent' | 'remove' | null>>({});
  const [applying, setApplying] = useState(false);

  const allDecided = substitutions.every(sub => decisions[sub.originalExerciseId] !== undefined && decisions[sub.originalExerciseId] !== null);

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const sub of substitutions) {
        const decision = decisions[sub.originalExerciseId];
        if (decision === 'remove') {
          await onRemove(sub);
        } else if (decision) {
          await onApply(sub, decision === 'permanent');
        }
      }
      onComplete();
    } catch (error) {
      console.error('Failed to apply substitutions:', error);
    } finally {
      setApplying(false);
    }
  };

  return (
    <Dialog open={true} onOpenChange={(open) => { if (!open) onComplete(); }}>
      <DialogContent className="max-w-lg max-h-[80vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="p-4 border-b bg-yellow-100 dark:bg-yellow-900">
          <div className="flex items-center gap-2 text-yellow-800 dark:text-yellow-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <DialogTitle>Exercise Substitutions Detected</DialogTitle>
          </div>
          <DialogDescription className="text-sm text-yellow-700 dark:text-yellow-300 mt-1">
            You used different exercises in Hevy. Choose how to handle each:
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {substitutions.map((sub) => (
            <div key={sub.originalExerciseId} className="border border-border rounded-lg p-3 bg-zinc-50 dark:bg-zinc-800">
              <div className="mb-3">
                <div className="flex items-center gap-2 text-sm">
                  <span className="text-zinc-500 dark:text-zinc-400">Program:</span>
                  <span className="font-medium line-through text-red-600 dark:text-red-400">{sub.originalExerciseName}</span>
                </div>
                <div className="flex items-center gap-2 text-sm mt-1">
                  <span className="text-zinc-500 dark:text-zinc-400">Hevy:</span>
                  <span className="font-medium text-green-600 dark:text-green-400">{sub.hevyExerciseName}</span>
                </div>
                <div className="text-xs text-zinc-500 dark:text-zinc-400 mt-1">
                  {sub.sets.length} sets: {sub.sets.map(s => `${s.weight}kg × ${s.reps}`).join(', ')}
                </div>
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'temporary' }))}
                  aria-pressed={decisions[sub.originalExerciseId] === 'temporary'}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'temporary'
                      ? 'bg-blue-600 text-white border-blue-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  This Session Only
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'permanent' }))}
                  aria-pressed={decisions[sub.originalExerciseId] === 'permanent'}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'permanent'
                      ? 'bg-green-600 text-white border-green-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Permanent Change
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'remove' }))}
                  aria-pressed={decisions[sub.originalExerciseId] === 'remove'}
                  className={`px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'remove'
                      ? 'bg-red-600 text-white border-red-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-red-50 dark:hover:bg-red-900/30 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Remove
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
