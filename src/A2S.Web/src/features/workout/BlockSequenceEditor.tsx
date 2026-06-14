import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { useUpdateBlockSequence } from "@/hooks/useWorkouts";
import type { WorkoutDto } from "@/types/workout";
import { getBlockColor } from "@/lib/blockColors";
import toast from "react-hot-toast";

interface BlockSequenceEditorProps {
  workout: WorkoutDto;
  isOpen: boolean;
  onClose: () => void;
  onUpdated: () => void;
}

const BLOCK_LABELS: Record<number, string> = {
  1: "Block 1 (75-90%)",
  2: "Block 2 (85-95%)",
  3: "Block 3 (90-105%)",
};

/**
 * Inline style for a block chip: tinted background + the block's identity colour as
 * text/border. Uses the shared block palette (lib/blockColors) so it renders identically
 * across themes — replaces the old Tailwind `dark:` class map, which only changed on OSRS.
 */
function blockChipStyle(blockType: number): React.CSSProperties {
  const color = getBlockColor(blockType);
  return {
    backgroundColor: `color-mix(in srgb, ${color} 18%, transparent)`,
    color,
    borderColor: `color-mix(in srgb, ${color} 45%, transparent)`,
  };
}

export function BlockSequenceEditor({ workout, isOpen, onClose, onUpdated }: BlockSequenceEditorProps) {
  const currentSequence = workout.blockSequence ?? [1, 2, 3];
  const [sequence, setSequence] = useState<number[]>([...currentSequence]);
  const updateBlockSequence = useUpdateBlockSequence();

  const currentBlockIndex = Math.floor((workout.currentWeek - 1) / 7);
  const minBlocks = currentBlockIndex + 1; // Can't shorten past current position
  const maxBlocks = 10;

  const handleAddBlock = (blockType: number) => {
    if (sequence.length >= maxBlocks) {
      toast.error(`Maximum ${maxBlocks} blocks (${maxBlocks * 7} weeks)`);
      return;
    }
    setSequence([...sequence, blockType]);
  };

  const handleRemoveBlock = (index: number) => {
    if (index < minBlocks) {
      toast.error("Cannot remove a block that has already been started");
      return;
    }
    if (sequence.length <= 1) {
      toast.error("Must have at least one block");
      return;
    }
    setSequence(sequence.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    try {
      await updateBlockSequence.mutateAsync({
        workoutId: workout.id,
        blockSequence: sequence,
      });
      toast.success(`Block sequence updated! Program is now ${sequence.length * 7} weeks.`);
      onUpdated();
      onClose();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to update block sequence";
      toast.error(message);
    }
  };

  const hasChanges = JSON.stringify(sequence) !== JSON.stringify(currentSequence);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={onClose}>
      <Card className="p-6 max-w-lg w-full mx-4 max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <h2 className="text-xl font-bold mb-4">Manage Block Sequence</h2>

        <p className="text-sm text-muted-foreground mb-4">
          Each block spans 7 weeks. Add, remove, or reorder blocks to customize your program length.
          Progression continues seamlessly between blocks.
        </p>

        {/* Current sequence visual */}
        <div className="mb-6">
          <div className="text-sm font-medium mb-2">Current Sequence:</div>
          <div className="flex flex-wrap gap-2">
            {sequence.map((blockType, idx) => {
              const isPast = idx < currentBlockIndex;
              const isCurrent = idx === currentBlockIndex;
              const canRemove = idx >= minBlocks;

              return (
                <div
                  key={idx}
                  style={blockChipStyle(blockType)}
                  className={`relative flex items-center gap-1 px-3 py-2 rounded-lg border text-sm font-medium transition-all ${
                    isCurrent ? "ring-2 ring-primary" : ""
                  } ${isPast ? "opacity-60" : ""}`}
                >
                  <span>B{blockType}</span>
                  {isCurrent && (
                    <span className="text-[10px] ml-1">(current)</span>
                  )}
                  {isPast && (
                    <span className="text-[10px] ml-1">(done)</span>
                  )}
                  {canRemove && (
                    <button
                      onClick={() => handleRemoveBlock(idx)}
                      className="ml-1 p-0.5 rounded-full hover:bg-black/10 dark:hover:bg-white/10 transition-colors"
                      title="Remove block"
                    >
                      <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                      </svg>
                    </button>
                  )}
                </div>
              );
            })}
          </div>
          <div className="text-xs text-muted-foreground mt-2">
            {sequence.length} blocks = {sequence.length * 7} weeks total
          </div>
        </div>

        {/* Add block buttons */}
        <div className="mb-6">
          <div className="text-sm font-medium mb-2">Add Block:</div>
          <div className="flex gap-2">
            {[1, 2, 3].map((blockType) => (
              <Button
                key={blockType}
                variant="outline"
                size="sm"
                onClick={() => handleAddBlock(blockType)}
                disabled={sequence.length >= maxBlocks}
                className="flex-1"
              >
                + {BLOCK_LABELS[blockType]}
              </Button>
            ))}
          </div>
        </div>

        {/* Block descriptions */}
        <div className="mb-6 text-xs text-muted-foreground space-y-1">
          <div><strong>Block 1:</strong> Volume phase - higher reps, moderate intensity (75-90%)</div>
          <div><strong>Block 2:</strong> Strength phase - moderate reps, higher intensity (85-95%)</div>
          <div><strong>Block 3:</strong> Peak phase - low reps, peak intensity (90-105%)</div>
          <div className="mt-2">Every 7th week in each block is a deload week (65%).</div>
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={!hasChanges || updateBlockSequence.isPending}
          >
            {updateBlockSequence.isPending ? "Saving..." : "Save Changes"}
          </Button>
        </div>
      </Card>
    </div>
  );
}
