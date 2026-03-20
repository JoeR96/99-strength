import { useState } from "react";
import { Button } from "@/components/ui/button";

interface UndoConfirmationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => Promise<void>;
  dayNumber: number;
  weekNumber: number;
  wouldRollbackWeek?: boolean;
}

export function UndoConfirmationModal({
  isOpen,
  onClose,
  onConfirm,
  dayNumber,
  weekNumber,
  wouldRollbackWeek = false,
}: UndoConfirmationModalProps) {
  const [isUndoing, setIsUndoing] = useState(false);

  const handleConfirm = async () => {
    setIsUndoing(true);
    try {
      await onConfirm();
      onClose();
    } catch (error) {
      // Error handling done in parent
    } finally {
      setIsUndoing(false);
    }
  };

  if (!isOpen) return null;

  const dayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden">
      {/* Backdrop - fully opaque dark background */}
      <div className="absolute inset-0 bg-black/80" onClick={onClose} />

      {/* Modal */}
      <div className="relative bg-white dark:bg-zinc-900 rounded-lg shadow-2xl w-full max-w-md mx-4 border border-border">
        {/* Header */}
        <div className="p-4 border-b border-border">
          <h2 className="text-lg font-semibold">Undo Last Workout?</h2>
        </div>

        {/* Content */}
        <div className="p-4 space-y-4">
          <p className="text-sm text-muted-foreground">
            This will undo your completion of <strong>{dayName}</strong> (Week {weekNumber})
            and restore all exercise progression to their previous state.
          </p>

          {wouldRollbackWeek && (
            <div className="p-3 bg-yellow-100 dark:bg-yellow-900/30 rounded-lg border border-yellow-200 dark:border-yellow-800">
              <div className="flex items-start gap-2">
                <svg className="h-5 w-5 text-yellow-600 dark:text-yellow-400 mt-0.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
                <div>
                  <p className="text-sm font-medium text-yellow-800 dark:text-yellow-200">
                    Week Rollback
                  </p>
                  <p className="text-xs text-yellow-700 dark:text-yellow-300 mt-1">
                    This will also roll back to Week {weekNumber} since you've already progressed to the next week.
                  </p>
                </div>
              </div>
            </div>
          )}

          <div className="text-sm text-muted-foreground">
            <strong>What will happen:</strong>
            <ul className="list-disc list-inside mt-2 space-y-1">
              <li>Training max adjustments will be reverted</li>
              <li>Set/rep progressions will be restored</li>
              <li>The day will be available to complete again</li>
              {wouldRollbackWeek && <li>Current week will change from {weekNumber + 1} to {weekNumber}</li>}
            </ul>
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-border flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={isUndoing}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={handleConfirm}
            disabled={isUndoing}
          >
            {isUndoing ? "Undoing..." : "Undo Workout"}
          </Button>
        </div>
      </div>
    </div>
  );
}
