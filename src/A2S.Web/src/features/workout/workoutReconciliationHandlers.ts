import type { Dispatch, SetStateAction } from "react";
import toast from "react-hot-toast";
import { workoutsApi } from "@/api/workouts";
import { getWeekParameters, roundToGymIncrement } from "@/utils/weekParameters";
import type { SubstituteExerciseRequest, UpdateExercisesRequest, WorkoutDto } from "@/types/workout";
import type {
  SetEntry,
  ExerciseEntry,
  DetectedSubstitution,
  WeightDiscrepancy,
  MissingExercise,
} from "./workoutSessionTypes";

export interface ReconciliationDeps {
  workout: WorkoutDto | null | undefined;
  exerciseEntries: ExerciseEntry[];
  setExerciseEntries: Dispatch<SetStateAction<ExerciseEntry[]>>;
  setTemporarySubstitutions: Dispatch<SetStateAction<{ originalExerciseId: string; originalName: string; substituteName: string }[]>>;
  setIsPrefilled: Dispatch<SetStateAction<boolean>>;
  substituteExercise: { mutateAsync: (args: { workoutId: string; request: SubstituteExerciseRequest }) => Promise<unknown> };
  updateExercisesMutation: { mutateAsync: (args: { workoutId: string; request: UpdateExercisesRequest }) => Promise<unknown> };
  removeExerciseMutation: { mutateAsync: (args: { workoutId: string; exerciseId: string }) => Promise<unknown> };
  refetch: () => Promise<{ data: WorkoutDto | null | undefined }>;
  convertWeightFromKg: (weightKg: number, targetUnit: string) => number;
}

export function createApplySubstitutionHandler(deps: ReconciliationDeps) {
  return async (sub: DetectedSubstitution, isPermanent: boolean) => {
    const entryIndex = deps.exerciseEntries.findIndex((e) => e.exercise.id === sub.originalExerciseId);
    if (entryIndex === -1) return;
    const entry = deps.exerciseEntries[entryIndex];
    const newSets: SetEntry[] = sub.sets.map((pulledSet, index) => ({
      setNumber: pulledSet.setNumber,
      weight: deps.convertWeightFromKg(pulledSet.weight, entry.weightUnit),
      reps: pulledSet.reps,
      isAmrap: entry.isAmrapExercise && index === sub.sets.length - 1,
      completed: true,
    }));

    if (isPermanent && deps.workout) {
      try {
        await deps.substituteExercise.mutateAsync({
          workoutId: deps.workout.id,
          request: {
            exerciseId: sub.originalExerciseId,
            newExerciseName: sub.hevyExerciseName,
            newHevyExerciseTemplateId: sub.hevyTemplateId,
            reason: "Pulled from Hevy workout",
          },
        });
        deps.setExerciseEntries((prev) =>
          prev.map((e, i) => i === entryIndex ? { ...e, exercise: { ...e.exercise, name: sub.hevyExerciseName }, sets: newSets } : e)
        );
        toast.success(`Permanently replaced "${sub.originalExerciseName}" with "${sub.hevyExerciseName}"`);
        await deps.refetch();
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to substitute exercise";
        toast.error(message);
        return;
      }
    } else {
      deps.setTemporarySubstitutions((prev) => [
        ...prev.filter((s) => s.originalExerciseId !== sub.originalExerciseId),
        { originalExerciseId: sub.originalExerciseId, originalName: sub.originalExerciseName, substituteName: sub.hevyExerciseName },
      ]);
      deps.setExerciseEntries((prev) =>
        prev.map((e, i) => i === entryIndex ? { ...e, exercise: { ...e.exercise, name: sub.hevyExerciseName }, sets: newSets } : e)
      );
      toast.success(`Substituted "${sub.originalExerciseName}" with "${sub.hevyExerciseName}" for this session`);
    }
  };
}

export function createRemoveFromSubstitutionHandler(deps: ReconciliationDeps) {
  return async (sub: DetectedSubstitution) => {
    if (!deps.workout) return;
    try {
      await deps.removeExerciseMutation.mutateAsync({ workoutId: deps.workout.id, exerciseId: sub.originalExerciseId });
      deps.setExerciseEntries((prev) => prev.filter((e) => e.exercise.id !== sub.originalExerciseId));
      toast.success(`Removed "${sub.originalExerciseName}" from program`);
      await deps.refetch();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to remove exercise";
      toast.error(message);
    }
  };
}

export function createApplyWeightDiscrepancyHandler(deps: ReconciliationDeps) {
  return async (discrepancy: WeightDiscrepancy, confirmedWeight: number, decision: 'skip' | 'update') => {
    if (decision === 'skip') {
      deps.setExerciseEntries((prev) =>
        prev.map((entry) => entry.exercise.id === discrepancy.exerciseId ? { ...entry, skipProgression: true } : entry)
      );
      toast.success(`Will skip progression for "${discrepancy.exerciseName}" this week`);
    } else if (decision === 'update' && deps.workout) {
      try {
        if (discrepancy.progressionType === 'Linear') {
          const weekParams = getWeekParameters(deps.workout.currentWeek);
          const newTm = roundToGymIncrement(confirmedWeight / weekParams.intensity, 'kg');
          await deps.updateExercisesMutation.mutateAsync({
            workoutId: deps.workout.id,
            request: {
              updates: [{
                exerciseId: discrepancy.exerciseId,
                trainingMaxValue: newTm,
                trainingMaxUnit: 1,
                reason: `Updated TM from Hevy sync: actual weight ${confirmedWeight}kg at ${Math.round(weekParams.intensity * 100)}% intensity → TM ${newTm}kg`,
              }],
            },
          });
          toast.success(`Updated Training Max for "${discrepancy.exerciseName}" to ${newTm}kg`);
          await deps.refetch();
        } else {
          await workoutsApi.updateWorkingWeight(deps.workout.id, discrepancy.exerciseId, confirmedWeight, 1, 'Updated from Hevy sync - weight discrepancy');
          toast.success(`Updated working weight for "${discrepancy.exerciseName}"`);
        }
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to update weight";
        toast.error(message);
        return;
      }
    }
  };
}

export function createMissingExerciseHandler(deps: ReconciliationDeps) {
  return async (exercise: MissingExercise, decision: 'delete' | 'skip') => {
    if (decision === 'delete' || decision === 'skip') {
      deps.setExerciseEntries((prev) =>
        prev.map((entry) => entry.exercise.id === exercise.exerciseId ? { ...entry, sets: [], skipProgression: true } : entry)
      );
    }
  };
}
