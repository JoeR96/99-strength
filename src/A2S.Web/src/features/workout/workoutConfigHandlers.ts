import type { Dispatch, SetStateAction } from "react";
import toast from "react-hot-toast";
import { hevyApi } from "@/services/hevyApi";
import { syncDayAsRoutine, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { workoutsApi } from "@/api/workouts";
import { getWeekParameters } from "@/utils/weekParameters";
import type {
  SetEntry,
  ExerciseEntry,
  DayNumber,
  ProgressionConfigRequest,
} from "./workoutSessionTypes";
import type { ExerciseConfigUpdate } from "./EditExerciseConfigModal";
import type { RepsPerSetConfig } from "./ExerciseSubstitutionModal";
import type { ExerciseUpdateRequest, ExerciseTemplate, SubstituteExerciseRequest, WorkoutDto } from "@/types/workout";
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from "@/types/workout";

export interface WorkoutConfigDeps {
  workout: WorkoutDto | null | undefined;
  dayNumber: DayNumber;
  refetch: () => Promise<{ data: WorkoutDto | null | undefined }>;
  setExerciseEntries: Dispatch<SetStateAction<ExerciseEntry[]>>;
  setTemporarySubstitutions: Dispatch<SetStateAction<{ originalExerciseId: string; originalName: string; substituteName: string }[]>>;
  substituteExercise: { mutateAsync: (args: { workoutId: string; request: SubstituteExerciseRequest }) => Promise<unknown> };
  updateExercisesMutation: { mutateAsync: (args: { workoutId: string; request: { updates: ExerciseUpdateRequest[] } }) => Promise<unknown> };
}

async function syncRoutineAfterChange(
  deps: WorkoutConfigDeps,
  toastId: string,
  successMsg: string,
  failureMsg: string
) {
  const { workout, dayNumber, refetch } = deps;
  if (!workout) return;

  const syncKey = `week${workout.currentWeek}-day${dayNumber}`;
  const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];
  if (existingRoutineId) {
    try { await hevyApi.deleteRoutine(existingRoutineId); } catch (error) { console.warn('Failed to delete Hevy routine:', error); }
  }
  let folderId = workout.hevyRoutineFolderId;
  if (!folderId) {
    const folderResult = await getOrCreateRoutineFolder(workout.name);
    if (folderResult) {
      folderId = folderResult.folderId;
      try { await workoutsApi.setHevyFolderId(workout.id, folderId); } catch (error) { console.warn('Failed to set Hevy folder ID:', error); }
    }
  }
  const { data: updatedWorkout } = await refetch();
  if (updatedWorkout) {
    const result = await syncDayAsRoutine(updatedWorkout, dayNumber, folderId, true);
    if (result.success) {
      toast.success(successMsg, { id: toastId });
    } else {
      toast.error(`${failureMsg}: ${result.message}`, { id: toastId });
    }
  }
}

export function createTemporarySubstituteHandler(deps: WorkoutConfigDeps) {
  return (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    deps.setTemporarySubstitutions((prev) => [
      ...prev.filter((s) => s.originalExerciseId !== originalExercise.id),
      { originalExerciseId: originalExercise.id, originalName: originalExercise.name, substituteName: substituteTemplate.name },
    ]);
    deps.setExerciseEntries((prev) =>
      prev.map((entry) => {
        if (entry.exercise.id !== originalExercise.id) return entry;
        if (repsConfig) {
          const newSets: SetEntry[] = [];
          for (let i = 1; i <= repsConfig.sets; i++) {
            newSets.push({ setNumber: i, weight: repsConfig.startingWeight, reps: repsConfig.maxReps, isAmrap: false, completed: false });
          }
          return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name }, sets: newSets, targetSets: repsConfig.sets, targetReps: repsConfig.maxReps, targetWeight: repsConfig.startingWeight, isAmrapExercise: false };
        }
        return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name } };
      })
    );
    const message = repsConfig
      ? `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" (Reps Per Set: ${repsConfig.sets}×${repsConfig.minReps}-${repsConfig.maxReps})`
      : `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" for this session`;
    toast.success(message);
  };
}

export function createPermanentSubstituteHandler(deps: WorkoutConfigDeps) {
  return async (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    if (!deps.workout) return;
    try {
      await deps.substituteExercise.mutateAsync({
        workoutId: deps.workout.id,
        request: {
          exerciseId: originalExercise.id,
          newExerciseName: substituteTemplate.name,
          reason: repsConfig ? `User substitution - switched to RepsPerSet (${repsConfig.sets}×${repsConfig.minReps}-${repsConfig.maxReps})` : "User substitution",
          newProgressionConfig: repsConfig ? { type: "RepsPerSet", repRangeMinimum: repsConfig.minReps, repRangeMaximum: repsConfig.maxReps, startingWeight: repsConfig.startingWeight, weightUnit: 1, targetSets: repsConfig.sets } : undefined,
        },
      });
      deps.setExerciseEntries((prev) =>
        prev.map((entry) => {
          if (entry.exercise.id !== originalExercise.id) return entry;
          if (repsConfig) {
            const newSets: SetEntry[] = [];
            for (let i = 1; i <= repsConfig.sets; i++) {
              newSets.push({ setNumber: i, weight: repsConfig.startingWeight, reps: repsConfig.maxReps, isAmrap: false, completed: false });
            }
            return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name }, sets: newSets, targetSets: repsConfig.sets, targetReps: repsConfig.maxReps, targetWeight: repsConfig.startingWeight, isAmrapExercise: false };
          }
          return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name } };
        })
      );
      const message = repsConfig
        ? `Permanently replaced "${originalExercise.name}" with "${substituteTemplate.name}" (Reps Per Set progression)`
        : `Permanently replaced "${originalExercise.name}" with "${substituteTemplate.name}"`;
      toast.success(message);
      await deps.refetch();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to substitute exercise";
      toast.error(message);
    }
  };
}

export function createSaveExerciseConfigHandler(deps: WorkoutConfigDeps) {
  return async (exerciseId: string, config: ExerciseConfigUpdate) => {
    if (!deps.workout) return;
    try {
      toast.loading("Saving changes...", { id: "save-exercise-config" });
      const updateRequest: ExerciseUpdateRequest = { exerciseId, reason: "Manual configuration update" };
      if (config.trainingMaxValue !== undefined) { updateRequest.trainingMaxValue = config.trainingMaxValue; updateRequest.trainingMaxUnit = config.trainingMaxUnit; }
      if (config.weightValue !== undefined) { updateRequest.weightValue = config.weightValue; updateRequest.weightUnit = config.weightUnit; }

      await deps.updateExercisesMutation.mutateAsync({ workoutId: deps.workout.id, request: { updates: [updateRequest] } });

      if (hevyApi.isConfigured()) {
        await syncRoutineAfterChange(
          deps,
          "save-exercise-config",
          "Exercise updated and Hevy routine refreshed!",
          "Exercise updated but Hevy sync failed"
        );
      } else {
        toast.success("Exercise configuration saved!", { id: "save-exercise-config" });
        await deps.refetch();
      }

      const { data: refreshedWorkout } = await deps.refetch();
      if (refreshedWorkout) {
        const updatedExercise = refreshedWorkout.exercises.find((e: ExerciseDto) => e.id === exerciseId);
        if (updatedExercise) {
          deps.setExerciseEntries(prev => prev.map(entry => {
            if (entry.exercise.id !== exerciseId) return entry;
            const isRepsPerSet = updatedExercise.progression.type === "RepsPerSet";
            const repsPerSetProg = isRepsPerSet ? (updatedExercise.progression as RepsPerSetProgressionDto) : null;
            const previousUnilateral = (entry.exercise.progression as RepsPerSetProgressionDto)?.isUnilateral;
            const newUnilateral = repsPerSetProg?.isUnilateral;
            if (isRepsPerSet && previousUnilateral !== newUnilateral) {
              const currentSetCount = entry.sets.length;
              let newSets: SetEntry[];
              if (newUnilateral && !previousUnilateral) {
                newSets = [];
                for (let i = 0; i < currentSetCount * 2; i++) {
                  const sourceSet = entry.sets[Math.floor(i / 2)];
                  newSets.push({ setNumber: i + 1, weight: config.weightValue ?? sourceSet.weight, reps: sourceSet.reps, isAmrap: false, completed: false });
                }
              } else if (!newUnilateral && previousUnilateral) {
                const newSetCount = Math.ceil(currentSetCount / 2);
                newSets = entry.sets.slice(0, newSetCount).map((set, i) => ({ ...set, setNumber: i + 1, weight: config.weightValue ?? set.weight }));
              } else {
                newSets = entry.sets.map(set => ({ ...set, weight: config.weightValue ?? set.weight }));
              }
              return { ...entry, exercise: updatedExercise, sets: newSets, targetSets: newSets.length, targetWeight: config.weightValue ?? entry.targetWeight };
            }
            return { ...entry, exercise: updatedExercise, sets: entry.sets.map(set => ({ ...set, weight: config.weightValue ?? config.trainingMaxValue ?? set.weight })), targetWeight: config.weightValue ?? config.trainingMaxValue ?? entry.targetWeight };
          }));
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to save changes";
      toast.error(message, { id: "save-exercise-config" });
      throw error;
    }
  };
}

export function createChangeProgressionHandler(deps: WorkoutConfigDeps) {
  return async (exerciseId: string, config: ProgressionConfigRequest) => {
    if (!deps.workout) return;
    const exercise = deps.workout.exercises.find((e: ExerciseDto) => e.id === exerciseId);
    if (!exercise) return;
    try {
      toast.loading("Changing progression...", { id: "change-progression" });
      await deps.substituteExercise.mutateAsync({
        workoutId: deps.workout.id,
        request: { exerciseId, newExerciseName: exercise.name, reason: `Changed progression from ${exercise.progression.type} to ${config.type}`, newProgressionConfig: config },
      });

      if (hevyApi.isConfigured()) {
        await syncRoutineAfterChange(
          deps,
          "change-progression",
          `Changed ${exercise.name} to ${config.type} progression. Hevy routine updated!`,
          "Progression changed but Hevy sync failed"
        );
      } else {
        toast.success(`Changed ${exercise.name} to ${config.type} progression`, { id: "change-progression" });
      }

      const { data: refreshedWorkout } = await deps.refetch();
      if (refreshedWorkout) {
        const updatedExercise = refreshedWorkout.exercises.find((e: ExerciseDto) => e.id === exerciseId);
        if (updatedExercise) {
          deps.setExerciseEntries(prev => prev.map(entry => {
            if (entry.exercise.id !== exerciseId) return entry;
            const isLinear = updatedExercise.progression.type === "Linear";
            const isRepsPerSet = updatedExercise.progression.type === "RepsPerSet";
            const linearProg = isLinear ? (updatedExercise.progression as LinearProgressionDto) : null;
            const rpsProgression = isRepsPerSet ? (updatedExercise.progression as RepsPerSetProgressionDto) : null;
            let newSets: SetEntry[];
            let newTargetSets: number, newTargetReps: number, newTargetWeight: number, newIsAmrap: boolean;

            if (isLinear && linearProg) {
              const weekParams = getWeekParameters(deps.workout!.currentWeek);
              newTargetSets = weekParams.sets;
              newTargetReps = weekParams.targetReps;
              newTargetWeight = Math.round((linearProg.trainingMax.value * weekParams.intensity / 100) / 2.5) * 2.5;
              newIsAmrap = linearProg.useAmrap;
              newSets = Array.from({ length: newTargetSets }, (_, i) => ({ setNumber: i + 1, weight: newTargetWeight, reps: newTargetReps, isAmrap: newIsAmrap && i === newTargetSets - 1, completed: false }));
            } else if (isRepsPerSet && rpsProgression) {
              newTargetSets = rpsProgression.currentSetCount;
              newTargetReps = rpsProgression.repRange.maximum;
              newTargetWeight = rpsProgression.currentWeight;
              newIsAmrap = false;
              newSets = Array.from({ length: newTargetSets }, (_, i) => ({ setNumber: i + 1, weight: newTargetWeight, reps: newTargetReps, isAmrap: false, completed: false }));
            } else {
              const minProg = updatedExercise.progression as MinimalSetsProgressionDto;
              newTargetSets = minProg?.currentSetCount ?? 4;
              newTargetReps = minProg ? Math.ceil(minProg.targetTotalReps / newTargetSets) : 10;
              newTargetWeight = minProg?.currentWeight ?? 0;
              newIsAmrap = false;
              newSets = Array.from({ length: newTargetSets }, (_, i) => ({ setNumber: i + 1, weight: newTargetWeight, reps: newTargetReps, isAmrap: false, completed: false }));
            }
            return { ...entry, exercise: updatedExercise, sets: newSets, targetSets: newTargetSets, targetReps: newTargetReps, targetWeight: newTargetWeight, isAmrapExercise: newIsAmrap, weightUnit: rpsProgression?.weightUnit ?? (linearProg?.trainingMax.unit === 2 ? "Pounds" : "Kilograms") };
          }));
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to change progression";
      toast.error(message, { id: "change-progression" });
    }
  };
}
