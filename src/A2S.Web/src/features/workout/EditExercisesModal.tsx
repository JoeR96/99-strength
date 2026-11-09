import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ConfirmModal } from "@/components/shared/ConfirmModal";
import { useUpdateExercises, useSubstituteExercise, useRemoveExercise } from "@/hooks/useWorkouts";
import { useSyncExerciseEditsToHevy } from "./useSyncExerciseEditsToHevy";
import { ExerciseEditRow } from "./ExerciseEditRow";
import {
  deriveExerciseEditStates,
  withRecalculatedHasChanged,
  type ExerciseEditState,
} from "./deriveExerciseEditStates";
import type {
  RepsPerSetProgressionDto,
  ExerciseUpdateRequest,
  WorkoutDto,
  ProgressionConfigRequest,
} from "@/types/workout";
import toast from "react-hot-toast";

interface EditExercisesModalProps {
  workout: WorkoutDto;
  day: number;
  isOpen: boolean;
  onClose: () => void;
  onSyncRequired?: () => void;
}

export function EditExercisesModal({ workout, day, isOpen, onClose, onSyncRequired }: EditExercisesModalProps) {
  const updateExercises = useUpdateExercises();
  const substituteExercise = useSubstituteExercise();
  const removeExerciseMutation = useRemoveExercise();
  const { syncExerciseEditsToHevy } = useSyncExerciseEditsToHevy();
  const [editStates, setEditStates] = useState<ExerciseEditState[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  // Track which exercises are expanded for editing
  const [expandedExercise, setExpandedExercise] = useState<string | null>(null);
  const [exerciseToRemove, setExerciseToRemove] = useState<{ id: string; name: string } | null>(null);

  const exercisesForDay = workout.exercises.filter((e) => e.assignedDay === day);

  useEffect(() => {
    if (isOpen) {
      setEditStates(deriveExerciseEditStates(exercisesForDay));
      setError(null);
      setExpandedExercise(null);
    }
  }, [isOpen, day, workout.exercises]);

  const updateState = (exerciseId: string, updates: Partial<ExerciseEditState>) => {
    setEditStates((prev) =>
      prev.map((state) => {
        if (state.exerciseId !== exerciseId) return state;
        return withRecalculatedHasChanged({ ...state, ...updates });
      })
    );
  };

  const handleRemoveExercise = async (exerciseId: string, exerciseName: string) => {
    try {
      await removeExerciseMutation.mutateAsync({ workoutId: workout.id, exerciseId });
      setEditStates((prev) => prev.filter((s) => s.exerciseId !== exerciseId));
      setExerciseToRemove(null);
      toast.success(`Removed ${exerciseName} from workout`);
      onSyncRequired?.();
    } catch (err: any) {
      toast.error(err.message || "Failed to remove exercise");
    }
  };

  const handleSave = async () => {
    const changedExercises = editStates.filter((s) => s.hasChanged);
    if (changedExercises.length === 0) {
      onClose();
      return;
    }

    setIsSaving(true);
    setError(null);

    try {
      // Separate into swap exercises and regular update exercises
      const swapExercises = changedExercises.filter((s) => s.wantSwap);
      const regularExercises = changedExercises.filter((s) => !s.wantSwap);

      // Also check regular exercises for rep range or set count changes (need substitute API)
      const repRangeChanges = regularExercises.filter(
        (s) =>
          s.progressionType === "RepsPerSet" &&
          (s.repRangeMin !== s.originalRepRangeMin ||
            s.repRangeMax !== s.originalRepRangeMax ||
            s.startingSets !== s.originalStartingSets ||
            s.currentSets !== s.originalCurrentSets ||
            s.targetSets !== s.originalTargetSets)
      );
      const pureWeightChanges = regularExercises.filter(
        (s) => !repRangeChanges.includes(s)
      );

      // 1. Handle progression type swaps via substitute API
      for (const state of swapExercises) {
        let config: ProgressionConfigRequest;
        if (state.progressionType === "Linear") {
          // Swap Linear -> RepsPerSet
          config = {
            type: "RepsPerSet",
            startingWeight: state.swapWeight,
            weightUnit: state.weightUnit,
            repRangeMinimum: state.swapRepMin,
            repRangeMaximum: state.swapRepMax,
            targetSets: state.swapTargetSets,
            isUnilateral: state.swapIsUnilateral,
          };
        } else {
          // Swap RepsPerSet -> Linear
          config = {
            type: "Linear",
            trainingMaxValue: state.swapTrainingMax,
            trainingMaxUnit: state.weightUnit,
            useAmrap: true,
            baseSetsPerExercise: 4,
          };
        }

        await substituteExercise.mutateAsync({
          workoutId: workout.id,
          request: {
            exerciseId: state.exerciseId,
            newExerciseName: state.name,
            reason: `Swapped progression from ${state.progressionType} to ${config.type}`,
            newProgressionConfig: config,
          },
        });
      }

      // 2. Handle rep range or set count changes via substitute API (same type, new config)
      for (const state of repRangeChanges) {
        const config: ProgressionConfigRequest = {
          type: "RepsPerSet",
          startingWeight: state.newValue,
          weightUnit: state.weightUnit,
          repRangeMinimum: state.repRangeMin,
          repRangeMaximum: state.repRangeMax,
          targetSets: state.targetSets,
          startingSets: state.startingSets,
          currentSets: state.currentSets,
          isUnilateral: (workout.exercises.find(e => e.id === state.exerciseId)?.progression as RepsPerSetProgressionDto | undefined)?.isUnilateral ?? false,
        };

        await substituteExercise.mutateAsync({
          workoutId: workout.id,
          request: {
            exerciseId: state.exerciseId,
            newExerciseName: state.name,
            reason: "Updated rep range configuration",
            newProgressionConfig: config,
          },
        });
      }

      // 3. Handle pure weight/TM changes via update API
      if (pureWeightChanges.length > 0) {
        const updates: ExerciseUpdateRequest[] = pureWeightChanges.map((state) => {
          if (state.progressionType === "Linear") {
            return {
              exerciseId: state.exerciseId,
              trainingMaxValue: state.newValue,
              trainingMaxUnit: state.weightUnit,
              reason: "Manual adjustment",
            };
          } else {
            return {
              exerciseId: state.exerciseId,
              weightValue: state.newValue,
              weightUnit: state.weightUnit,
              reason: "Manual adjustment",
            };
          }
        });

        await updateExercises.mutateAsync({
          workoutId: workout.id,
          request: { updates },
        });
      }

      // 4. If Hevy is configured, delete old routine and push updated one
      await syncExerciseEditsToHevy(workout, day, onSyncRequired);

      onClose();
    } catch (err: any) {
      setError(err.message || "Failed to update exercises");
    } finally {
      setIsSaving(false);
    }
  };

  const hasChanges = editStates.some((s) => s.hasChanged);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <Card className="w-full max-w-2xl max-h-[80vh] overflow-y-auto m-4 p-6">
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-2xl font-bold">Edit Day {day} Exercises</h2>
          <Button variant="ghost" onClick={onClose} className="text-2xl p-2">
            &times;
          </Button>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-destructive/10 border border-destructive/20 rounded-lg text-destructive">
            {error}
          </div>
        )}

        <div className="space-y-4">
          {editStates.map((state) => (
            <ExerciseEditRow
              key={state.exerciseId}
              state={state}
              isExpanded={expandedExercise === state.exerciseId}
              onToggleExpanded={() =>
                setExpandedExercise(expandedExercise === state.exerciseId ? null : state.exerciseId)
              }
              onRemoveRequested={() => setExerciseToRemove({ id: state.exerciseId, name: state.name })}
              updateState={updateState}
            />
          ))}
        </div>

        <div className="flex justify-end gap-3 mt-6 pt-4 border-t">
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={!hasChanges || isSaving}
          >
            {isSaving ? "Saving..." : "Save Changes"}
          </Button>
        </div>
      </Card>

      {/* Remove exercise confirmation */}
      <ConfirmModal
        open={exerciseToRemove != null}
        onCancel={() => setExerciseToRemove(null)}
        onConfirm={() => exerciseToRemove && handleRemoveExercise(exerciseToRemove.id, exerciseToRemove.name)}
        title="Remove Exercise"
        body={<>Are you sure you want to permanently remove <strong>{exerciseToRemove?.name}</strong> from this workout?</>}
        confirmLabel="Remove"
      />
    </div>
  );
}
