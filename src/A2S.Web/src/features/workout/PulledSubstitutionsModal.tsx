import { useState } from "react";
import { ReviewModal } from "@/components/shared/ReviewModal";
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
    <ReviewModal
      open={true}
      onOpenChange={(open) => { if (!open) onComplete(); }}
      title="Exercise Substitutions Detected"
      description="You used different exercises in Hevy. Choose how to handle each:"
      headerClassName="bg-yellow-100 text-yellow-800"
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
      {substitutions.map((sub) => (
        <div key={sub.originalExerciseId} className="border border-border rounded-lg p-3 bg-zinc-50">
          <div className="mb-3">
            <div className="flex items-center gap-2 text-sm">
              <span className="text-zinc-500">Program:</span>
              <span className="font-medium line-through text-red-600">{sub.originalExerciseName}</span>
            </div>
            <div className="flex items-center gap-2 text-sm mt-1">
              <span className="text-zinc-500">Hevy:</span>
              <span className="font-medium text-green-600">{sub.hevyExerciseName}</span>
            </div>
            <div className="text-xs text-zinc-500 mt-1">
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
                  : 'bg-white hover:bg-zinc-100 border-zinc-300 text-zinc-700'
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
                  : 'bg-white hover:bg-zinc-100 border-zinc-300 text-zinc-700'
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
                  : 'bg-white hover:bg-red-50 border-zinc-300 text-zinc-700'
              }`}
            >
              Remove
            </button>
          </div>
        </div>
      ))}
    </ReviewModal>
  );
}
