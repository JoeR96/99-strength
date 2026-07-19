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
      headerClassName="bg-warning/15 text-warning"
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
        <div key={sub.originalExerciseId} className="border border-border rounded-lg p-3 bg-muted/30">
          <div className="mb-3">
            <div className="flex items-center gap-2 text-sm">
              <span className="text-muted-foreground">Program:</span>
              <span className="font-medium line-through text-destructive">{sub.originalExerciseName}</span>
            </div>
            <div className="flex items-center gap-2 text-sm mt-1">
              <span className="text-muted-foreground">Hevy:</span>
              <span className="font-medium text-success">{sub.hevyExerciseName}</span>
            </div>
            <div className="text-xs text-muted-foreground mt-1">
              {sub.sets.length} sets: {sub.sets.map(s => `${s.weight}kg × ${s.reps}`).join(', ')}
            </div>
          </div>

          <div className="flex gap-2">
            <button
              onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'temporary' }))}
              aria-pressed={decisions[sub.originalExerciseId] === 'temporary'}
              className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                decisions[sub.originalExerciseId] === 'temporary'
                  ? 'bg-neon-blue text-background border-neon-blue'
                  : 'bg-card hover:bg-muted border-border text-foreground'
              }`}
            >
              This Session Only
            </button>
            <button
              onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'permanent' }))}
              aria-pressed={decisions[sub.originalExerciseId] === 'permanent'}
              className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                decisions[sub.originalExerciseId] === 'permanent'
                  ? 'bg-success text-success-foreground border-success'
                  : 'bg-card hover:bg-muted border-border text-foreground'
              }`}
            >
              Permanent Change
            </button>
            <button
              onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'remove' }))}
              aria-pressed={decisions[sub.originalExerciseId] === 'remove'}
              className={`px-3 py-2 text-sm rounded border transition-colors font-medium ${
                decisions[sub.originalExerciseId] === 'remove'
                  ? 'bg-destructive text-background border-destructive'
                  : 'bg-card hover:bg-destructive/10 border-border text-foreground'
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
