import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useUpdateExercises } from "@/hooks/useWorkouts";
import type {
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  ExerciseUpdateRequest,
  WorkoutDto,
} from "@/types/workout";
import { WeightUnit } from "@/types/workout";

interface EditExercisesModalProps {
  workout: WorkoutDto;
  day: number;
  isOpen: boolean;
  onClose: () => void;
  onSyncRequired?: () => void;
}

interface ExerciseEditState {
  exerciseId: string;
  name: string;
  progressionType: "Linear" | "RepsPerSet" | "MinimalSets";
  originalValue: number;
  newValue: number;
  unit: string;
  hasChanged: boolean;
}

export function EditExercisesModal({ workout, day, isOpen, onClose, onSyncRequired }: EditExercisesModalProps) {
  const updateExercises = useUpdateExercises();
  const [editStates, setEditStates] = useState<ExerciseEditState[]>([]);
  const [error, setError] = useState<string | null>(null);

  const exercisesForDay = workout.exercises.filter((e) => e.assignedDay === day);

  useEffect(() => {
    if (isOpen) {
      const states = exercisesForDay.map((exercise) => {
        const isLinear = exercise.progression.type === "Linear";
        const isRepsPerSet = exercise.progression.type === "RepsPerSet";
        const isMinimalSets = exercise.progression.type === "MinimalSets";

        let value = 0;
        let unit = "kg";

        if (isLinear) {
          const prog = exercise.progression as LinearProgressionDto;
          value = prog.trainingMax.value;
          unit = prog.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs";
        } else if (isRepsPerSet) {
          const prog = exercise.progression as RepsPerSetProgressionDto;
          value = prog.currentWeight;
          unit = prog.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
        } else if (isMinimalSets) {
          const prog = exercise.progression as MinimalSetsProgressionDto;
          value = prog.currentWeight;
          unit = prog.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
        }

        return {
          exerciseId: exercise.id,
          name: exercise.name,
          progressionType: exercise.progression.type,
          originalValue: value,
          newValue: value,
          unit,
          hasChanged: false,
        };
      });
      setEditStates(states);
      setError(null);
    }
  }, [isOpen, day, workout.exercises]);

  const handleValueChange = (exerciseId: string, newValue: number) => {
    setEditStates((prev) =>
      prev.map((state) =>
        state.exerciseId === exerciseId
          ? { ...state, newValue, hasChanged: newValue !== state.originalValue }
          : state
      )
    );
  };

  const handleSave = async () => {
    const changedExercises = editStates.filter((s) => s.hasChanged);
    if (changedExercises.length === 0) {
      onClose();
      return;
    }

    const updates: ExerciseUpdateRequest[] = changedExercises.map((state) => {
      const weightUnit = state.unit === "kg" ? WeightUnit.Kilograms : WeightUnit.Pounds;

      if (state.progressionType === "Linear") {
        return {
          exerciseId: state.exerciseId,
          trainingMaxValue: state.newValue,
          trainingMaxUnit: weightUnit,
          reason: "Manual adjustment",
        };
      } else {
        return {
          exerciseId: state.exerciseId,
          weightValue: state.newValue,
          weightUnit: weightUnit,
          reason: "Manual adjustment",
        };
      }
    });

    try {
      await updateExercises.mutateAsync({
        workoutId: workout.id,
        request: { updates },
      });

      // Notify that sync might be required
      if (onSyncRequired) {
        onSyncRequired();
      }

      onClose();
    } catch (err: any) {
      setError(err.message || "Failed to update exercises");
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

        <div className="space-y-6">
          {editStates.map((state) => (
            <div key={state.exerciseId} className="p-4 border rounded-lg bg-card/50">
              <div className="flex justify-between items-start mb-3">
                <div>
                  <h3 className="font-semibold text-lg">{state.name}</h3>
                  <span className="text-sm text-muted-foreground">
                    {state.progressionType === "Linear"
                      ? "Linear Progression (Training Max)"
                      : state.progressionType === "RepsPerSet"
                      ? "Reps Per Set (Current Weight)"
                      : "Minimal Sets (Current Weight)"}
                  </span>
                </div>
                {state.hasChanged && (
                  <span className="text-xs px-2 py-1 rounded-full bg-primary/20 text-primary">
                    Modified
                  </span>
                )}
              </div>

              <div className="flex items-center gap-4">
                <div className="flex-1">
                  <Label htmlFor={`weight-${state.exerciseId}`} className="text-sm text-muted-foreground">
                    {state.progressionType === "Linear" ? "Training Max" : "Weight"} ({state.unit})
                  </Label>
                  <div className="flex items-center gap-2 mt-1">
                    <Input
                      id={`weight-${state.exerciseId}`}
                      type="number"
                      step="0.5"
                      min="0"
                      value={state.newValue}
                      onChange={(e) => handleValueChange(state.exerciseId, parseFloat(e.target.value) || 0)}
                      className="w-32 text-lg font-medium"
                    />
                    <span className="text-muted-foreground">{state.unit}</span>
                    {state.hasChanged && (
                      <span className="text-sm text-muted-foreground">
                        (was {state.originalValue} {state.unit})
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="flex justify-end gap-3 mt-6 pt-4 border-t">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={!hasChanges || updateExercises.isPending}
          >
            {updateExercises.isPending ? "Saving..." : "Save Changes"}
          </Button>
        </div>

        {hasChanges && (
          <p className="text-sm text-muted-foreground mt-3 text-center">
            Note: After saving, you may need to re-sync your Hevy routines to reflect these changes.
          </p>
        )}
      </Card>
    </div>
  );
}
