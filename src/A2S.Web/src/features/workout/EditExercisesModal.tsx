import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useUpdateExercises, useSubstituteExercise, useRemoveExercise } from "@/hooks/useWorkouts";
import type {
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  ExerciseUpdateRequest,
  WorkoutDto,
  ProgressionConfigRequest,
} from "@/types/workout";
import { WeightUnit } from "@/types/workout";
import { hevyApi } from "@/services/hevyApi";
import { syncDayAsRoutine, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { workoutsApi } from "@/api/workouts";
import toast from "react-hot-toast";

interface EditExercisesModalProps {
  workout: WorkoutDto;
  day: number;
  isOpen: boolean;
  onClose: () => void;
  onSyncRequired?: () => void;
}

type ProgressionType = "Linear" | "RepsPerSet" | "MinimalSets";

interface ExerciseEditState {
  exerciseId: string;
  name: string;
  progressionType: ProgressionType;
  // Weight / TM
  originalValue: number;
  newValue: number;
  unit: string;
  weightUnit: WeightUnit;
  hasChanged: boolean;
  // RepsPerSet rep range
  repRangeMin: number;
  repRangeMax: number;
  originalRepRangeMin: number;
  originalRepRangeMax: number;
  isUnilateral: boolean;
  originalIsUnilateral: boolean;
  startingSets: number;
  originalStartingSets: number;
  currentSets: number;
  originalCurrentSets: number;
  targetSets: number;
  originalTargetSets: number;
  // Swap state
  wantSwap: boolean;
  // Swap fields: when Linear wants to become RPS
  swapWeight: number;
  swapRepMin: number;
  swapRepMax: number;
  swapTargetSets: number;
  swapIsUnilateral: boolean;
  // Swap fields: when RPS wants to become Linear
  swapTrainingMax: number;
  // Extra info for display
  linearSets?: number;
  linearAmrap?: boolean;
}

export function EditExercisesModal({ workout, day, isOpen, onClose, onSyncRequired }: EditExercisesModalProps) {
  const updateExercises = useUpdateExercises();
  const substituteExercise = useSubstituteExercise();
  const removeExerciseMutation = useRemoveExercise();
  const [editStates, setEditStates] = useState<ExerciseEditState[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  // Track which exercises are expanded for editing
  const [expandedExercise, setExpandedExercise] = useState<string | null>(null);
  const [exerciseToRemove, setExerciseToRemove] = useState<{ id: string; name: string } | null>(null);

  const exercisesForDay = workout.exercises.filter((e) => e.assignedDay === day);

  useEffect(() => {
    if (isOpen) {
      const states = exercisesForDay.map((exercise): ExerciseEditState => {
        const isLinear = exercise.progression.type === "Linear";
        const isRepsPerSet = exercise.progression.type === "RepsPerSet";
        const isMinimalSets = exercise.progression.type === "MinimalSets";

        let value = 0;
        let unit = "kg";
        let weightUnit: WeightUnit = WeightUnit.Kilograms;

        const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
        const rpsProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
        const minProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

        if (linearProg) {
          value = linearProg.trainingMax.value;
          weightUnit = linearProg.trainingMax.unit;
          unit = weightUnit === WeightUnit.Kilograms ? "kg" : "lbs";
        } else if (rpsProg) {
          value = rpsProg.currentWeight;
          unit = rpsProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
          weightUnit = unit === "lbs" ? WeightUnit.Pounds : WeightUnit.Kilograms;
        } else if (minProg) {
          value = minProg.currentWeight;
          unit = minProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
          weightUnit = unit === "lbs" ? WeightUnit.Pounds : WeightUnit.Kilograms;
        }

        return {
          exerciseId: exercise.id,
          name: exercise.name,
          progressionType: exercise.progression.type as ProgressionType,
          originalValue: value,
          newValue: value,
          unit,
          weightUnit,
          hasChanged: false,
          // Rep range
          repRangeMin: rpsProg?.repRange?.minimum ?? 8,
          repRangeMax: rpsProg?.repRange?.maximum ?? 12,
          originalRepRangeMin: rpsProg?.repRange?.minimum ?? 8,
          originalRepRangeMax: rpsProg?.repRange?.maximum ?? 12,
          isUnilateral: rpsProg?.isUnilateral ?? false,
          originalIsUnilateral: rpsProg?.isUnilateral ?? false,
          startingSets: rpsProg?.startingSets ?? 2,
          originalStartingSets: rpsProg?.startingSets ?? 2,
          currentSets: rpsProg?.currentSetCount ?? 2,
          originalCurrentSets: rpsProg?.currentSetCount ?? 2,
          targetSets: rpsProg?.targetSets ?? 5,
          originalTargetSets: rpsProg?.targetSets ?? 5,
          // Swap state
          wantSwap: false,
          // Swap to RPS defaults (from Linear: ~60% TM)
          swapWeight: linearProg ? Math.round((linearProg.trainingMax.value * 0.6) / 2.5) * 2.5 : value,
          swapRepMin: 8,
          swapRepMax: 12,
          swapTargetSets: 5,
          swapIsUnilateral: false,
          // Swap to Linear defaults (from RPS: ~150% weight)
          swapTrainingMax: rpsProg ? Math.round((rpsProg.currentWeight * 1.5) / 2.5) * 2.5 : 100,
          // Display info
          linearSets: linearProg?.baseSetsPerExercise,
          linearAmrap: linearProg?.useAmrap,
        };
      });
      setEditStates(states);
      setError(null);
      setExpandedExercise(null);
    }
  }, [isOpen, day, workout.exercises]);

  const updateState = (exerciseId: string, updates: Partial<ExerciseEditState>) => {
    setEditStates((prev) =>
      prev.map((state) => {
        if (state.exerciseId !== exerciseId) return state;
        const updated = { ...state, ...updates };
        // Recalculate hasChanged
        updated.hasChanged =
          updated.newValue !== updated.originalValue ||
          updated.repRangeMin !== updated.originalRepRangeMin ||
          updated.repRangeMax !== updated.originalRepRangeMax ||
          updated.isUnilateral !== updated.originalIsUnilateral ||
          updated.startingSets !== updated.originalStartingSets ||
          updated.currentSets !== updated.originalCurrentSets ||
          updated.targetSets !== updated.originalTargetSets ||
          updated.wantSwap;
        return updated;
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
          isUnilateral: state.isUnilateral,
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
              isUnilateral: state.isUnilateral !== state.originalIsUnilateral ? state.isUnilateral : undefined,
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
      if (hevyApi.isConfigured()) {
        toast.loading("Syncing to Hevy...", { id: "edit-exercises" });

        const syncKey = `week${workout.currentWeek}-day${day}`;
        const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

        // Delete existing routine first
        if (existingRoutineId) {
          try {
            await hevyApi.deleteRoutine(existingRoutineId);
            // Wait for Hevy API to process the deletion
            await new Promise((r) => setTimeout(r, 1000));
          } catch (deleteError) {
            console.warn("Failed to delete old routine:", deleteError);
          }
        }

        let folderId = workout.hevyRoutineFolderId;
        if (!folderId) {
          const folderResult = await getOrCreateRoutineFolder(workout.name);
          if (folderResult) {
            folderId = folderResult.folderId;
            try {
              await workoutsApi.setHevyFolderId(workout.id, folderId);
            } catch (err) {
              console.error("Failed to save folder ID:", err);
            }
          }
        }

        // Fetch latest workout data directly (don't rely on parent refetch timing)
        try {
          const latestWorkout = await workoutsApi.getCurrentWorkout();
          if (latestWorkout) {
            const result = await syncDayAsRoutine(latestWorkout, day as any, folderId, true);
            if (result.success) {
              toast.success("Exercises updated and Hevy routine refreshed!", { id: "edit-exercises" });
            } else {
              toast.error(`Exercises updated but Hevy sync failed: ${result.message}`, { id: "edit-exercises" });
            }
          } else {
            toast.success("Exercises updated! Re-sync to Hevy manually.", { id: "edit-exercises" });
          }
        } catch (syncErr) {
          console.error("Hevy sync error:", syncErr);
          toast.success("Exercises updated! Re-sync to Hevy to apply changes.", { id: "edit-exercises" });
        }

        // Notify parent to refetch workout data
        onSyncRequired?.();
      } else {
        onSyncRequired?.();
        toast.success("Exercises updated!");
      }

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
          {editStates.map((state) => {
            const isExpanded = expandedExercise === state.exerciseId;
            const isLinear = state.progressionType === "Linear" && !state.wantSwap;
            const isRepsPerSet = state.progressionType === "RepsPerSet" && !state.wantSwap;
            const swapTarget = state.progressionType === "Linear" ? "Reps Per Set" : "Linear (Hypertrophy)";

            return (
              <div
                key={state.exerciseId}
                className={`p-4 border rounded-lg transition-colors ${
                  state.hasChanged
                    ? "bg-primary/5 border-primary/30"
                    : "bg-card/50"
                }`}
              >
                {/* Exercise header - always visible */}
                <div className="flex justify-between items-start mb-3">
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-semibold text-lg">{state.name}</h3>
                      {state.hasChanged && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-primary/20 text-primary">
                          Modified
                        </span>
                      )}
                      {state.wantSwap && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-orange-200 text-orange-700 dark:bg-orange-900/30 dark:text-orange-300">
                          Swapping
                        </span>
                      )}
                    </div>
                    <span className="text-sm text-muted-foreground">
                      {state.wantSwap
                        ? `→ ${swapTarget}`
                        : state.progressionType === "Linear"
                        ? "Linear (Hypertrophy)"
                        : state.progressionType === "RepsPerSet"
                        ? "Reps Per Set"
                        : "Minimal Sets"}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => setExerciseToRemove({ id: state.exerciseId, name: state.name })}
                      className="p-1.5 hover:bg-destructive/10 rounded transition-colors text-muted-foreground hover:text-destructive"
                      title="Remove exercise"
                    >
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                    <button
                      onClick={() =>
                        setExpandedExercise(isExpanded ? null : state.exerciseId)
                      }
                      className="p-1.5 hover:bg-muted rounded transition-colors text-muted-foreground"
                      title={isExpanded ? "Collapse" : "Expand to edit"}
                    >
                      <svg
                        className={`w-4 h-4 transition-transform ${isExpanded ? "rotate-180" : ""}`}
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                      >
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                      </svg>
                    </button>
                  </div>
                </div>

                {/* Compact weight/TM input - always visible when not swapping */}
                {!state.wantSwap && (
                  <div className="flex items-center gap-3">
                    <Label className="text-sm text-muted-foreground whitespace-nowrap">
                      {isLinear ? "TM" : "Weight"}
                    </Label>
                    <Input
                      type="number"
                      step="any"
                      min="0"
                      value={state.newValue}
                      onChange={(e) => {
                        const val = e.target.value;
                        updateState(state.exerciseId, {
                          newValue: val === "" ? 0 : Number(val),
                        });
                      }}
                      className="w-24 text-base font-medium"
                    />
                    <span className="text-sm text-muted-foreground">{state.unit}</span>
                    {state.newValue !== state.originalValue && (
                      <span className="text-xs text-muted-foreground">
                        (was {state.originalValue})
                      </span>
                    )}
                  </div>
                )}

                {/* Expanded section */}
                {isExpanded && (
                  <div className="mt-4 pt-4 border-t border-border space-y-4">
                    {/* RepsPerSet: rep range + unilateral */}
                    {isRepsPerSet && (
                      <>
                        <div>
                          <Label className="text-sm font-medium mb-1 block">Rep Range</Label>
                          <div className="grid grid-cols-2 gap-2">
                            <div>
                              <label className="text-xs text-muted-foreground">Min</label>
                              <Input
                                type="number"
                                value={state.repRangeMin}
                                onChange={(e) =>
                                  updateState(state.exerciseId, {
                                    repRangeMin: Number(e.target.value),
                                  })
                                }
                                min={1}
                                max={30}
                              />
                            </div>
                            <div>
                              <label className="text-xs text-muted-foreground">Max</label>
                              <Input
                                type="number"
                                value={state.repRangeMax}
                                onChange={(e) =>
                                  updateState(state.exerciseId, {
                                    repRangeMax: Number(e.target.value),
                                  })
                                }
                                min={1}
                                max={30}
                              />
                            </div>
                          </div>
                        </div>

                        <div className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
                          <div>
                            <div className="font-medium text-sm">Unilateral</div>
                            <div className="text-xs text-muted-foreground">
                              One side at a time
                            </div>
                          </div>
                          <button
                            type="button"
                            onClick={() =>
                              updateState(state.exerciseId, {
                                isUnilateral: !state.isUnilateral,
                              })
                            }
                            className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                              state.isUnilateral ? "bg-primary" : "bg-muted"
                            }`}
                          >
                            <span
                              className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                                state.isUnilateral ? "translate-x-6" : "translate-x-1"
                              }`}
                            />
                          </button>
                        </div>

                        <div>
                          <Label className="text-sm font-medium mb-1 block">Sets</Label>
                          <div className="grid grid-cols-2 gap-2">
                            <div>
                              <label className="text-xs text-muted-foreground">Starting</label>
                              <Input
                                type="number"
                                value={state.startingSets}
                                onChange={(e) =>
                                  updateState(state.exerciseId, {
                                    startingSets: Number(e.target.value),
                                  })
                                }
                                min={1}
                                max={10}
                              />
                            </div>
                            <div>
                              <label className="text-xs text-muted-foreground">Current</label>
                              <Input
                                type="number"
                                value={state.currentSets}
                                onChange={(e) =>
                                  updateState(state.exerciseId, {
                                    currentSets: Number(e.target.value),
                                  })
                                }
                                min={1}
                                max={10}
                              />
                            </div>
                            <div>
                              <label className="text-xs text-muted-foreground">Target</label>
                              <Input
                                type="number"
                                value={state.targetSets}
                                onChange={(e) =>
                                  updateState(state.exerciseId, {
                                    targetSets: Number(e.target.value),
                                  })
                                }
                                min={1}
                                max={10}
                              />
                            </div>
                          </div>
                          {state.isUnilateral && (
                            <p className="text-xs text-muted-foreground mt-1">Per side</p>
                          )}
                        </div>
                      </>
                    )}

                    {/* Linear: show info */}
                    {isLinear && (
                      <div className="text-sm text-muted-foreground bg-muted/50 rounded p-3">
                        <div>Sets: {state.linearSets}</div>
                        <div>AMRAP: {state.linearAmrap ? "Yes" : "No"}</div>
                      </div>
                    )}

                    {/* Swap to RPS form (when Linear exercise wants to swap) */}
                    {state.wantSwap && state.progressionType === "Linear" && (
                      <div className="space-y-3 p-3 bg-orange-50 dark:bg-orange-950/20 rounded-lg border border-orange-200 dark:border-orange-800">
                        <p className="text-sm text-orange-700 dark:text-orange-300 font-medium">
                          Configure Reps Per Set
                        </p>
                        <div>
                          <Label className="text-sm">Starting Weight ({state.unit})</Label>
                          <Input
                            type="number"
                            step="2.5"
                            value={state.swapWeight}
                            onChange={(e) =>
                              updateState(state.exerciseId, {
                                swapWeight: parseFloat(e.target.value) || 0,
                              })
                            }
                            className="mt-1"
                          />
                        </div>
                        <div>
                          <Label className="text-sm">Rep Range</Label>
                          <div className="grid grid-cols-2 gap-2 mt-1">
                            <div>
                              <label className="text-xs text-muted-foreground">Min</label>
                              <Input
                                type="number"
                                value={state.swapRepMin}
                                onChange={(e) =>
                                  updateState(state.exerciseId, { swapRepMin: Number(e.target.value) })
                                }
                              />
                            </div>
                            <div>
                              <label className="text-xs text-muted-foreground">Max</label>
                              <Input
                                type="number"
                                value={state.swapRepMax}
                                onChange={(e) =>
                                  updateState(state.exerciseId, { swapRepMax: Number(e.target.value) })
                                }
                              />
                            </div>
                          </div>
                        </div>
                        <div>
                          <Label className="text-sm">Target Sets</Label>
                          <Input
                            type="number"
                            value={state.swapTargetSets}
                            onChange={(e) =>
                              updateState(state.exerciseId, { swapTargetSets: Number(e.target.value) })
                            }
                            min={1}
                            max={10}
                            className="mt-1"
                          />
                        </div>
                      </div>
                    )}

                    {/* Swap to Linear form (when RPS exercise wants to swap) */}
                    {state.wantSwap && state.progressionType === "RepsPerSet" && (
                      <div className="space-y-3 p-3 bg-orange-50 dark:bg-orange-950/20 rounded-lg border border-orange-200 dark:border-orange-800">
                        <p className="text-sm text-orange-700 dark:text-orange-300 font-medium">
                          Configure Linear (Hypertrophy)
                        </p>
                        <div>
                          <Label className="text-sm">Training Max ({state.unit})</Label>
                          <Input
                            type="number"
                            step="2.5"
                            value={state.swapTrainingMax}
                            onChange={(e) =>
                              updateState(state.exerciseId, {
                                swapTrainingMax: parseFloat(e.target.value) || 0,
                              })
                            }
                            className="mt-1"
                          />
                          <p className="text-xs text-muted-foreground mt-1">
                            ~90-95% of your 1RM
                          </p>
                        </div>
                      </div>
                    )}

                    {/* Swap toggle button - only for Linear and RepsPerSet */}
                    {(state.progressionType === "Linear" || state.progressionType === "RepsPerSet") && (
                      <button
                        type="button"
                        onClick={() =>
                          updateState(state.exerciseId, { wantSwap: !state.wantSwap })
                        }
                        className={`w-full px-3 py-2.5 text-sm font-medium rounded-lg border-2 transition-all flex items-center justify-center gap-2 ${
                          state.wantSwap
                            ? "border-orange-400 bg-orange-50 text-orange-700 dark:bg-orange-950/30 dark:text-orange-300 dark:border-orange-700"
                            : "border-border hover:border-primary/50 hover:bg-muted/50 text-muted-foreground"
                        }`}
                      >
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                        </svg>
                        {state.wantSwap
                          ? `Cancel — keep ${state.progressionType === "Linear" ? "Linear (Hypertrophy)" : "Reps Per Set"}`
                          : `Swap to ${swapTarget}`}
                      </button>
                    )}
                  </div>
                )}
              </div>
            );
          })}
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
      {exerciseToRemove && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/70 backdrop-blur-sm">
          <Card className="w-full max-w-sm m-4 p-6">
            <h3 className="text-lg font-bold mb-2">Remove Exercise</h3>
            <p className="text-sm text-muted-foreground mb-4">
              Are you sure you want to permanently remove <strong>{exerciseToRemove.name}</strong> from this workout?
            </p>
            <div className="flex justify-end gap-3">
              <Button variant="outline" onClick={() => setExerciseToRemove(null)}>
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={() => handleRemoveExercise(exerciseToRemove.id, exerciseToRemove.name)}
              >
                Remove
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
