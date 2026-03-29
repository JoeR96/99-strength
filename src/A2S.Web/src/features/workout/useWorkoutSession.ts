import { useState, useMemo, useRef, useEffect } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import { useCurrentWorkout, useSubstituteExercise, useUpdateExercises, useUndoCompletion, useRemoveExercise } from "@/hooks/useWorkouts";
import { workoutsApi } from "@/api/workouts";
import { useHevy } from "@/contexts/HevyContext";
import { hevyApi } from "@/services/hevyApi";
import { syncDayAsRoutine, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { getWeekParameters, roundToGymIncrement } from "@/utils/weekParameters";
import { kgToLbs } from "@/utils/constants";
import toast from "react-hot-toast";
import type {
  SetEntry,
  ExerciseEntry,
  TemporarySubstitution,
  SavedWorkoutProgress,
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  CompleteDayResult,
  DayNumber,
  DetectedSubstitution,
  WeightDiscrepancy,
  MissingExercise,
  PendingWeightExerciseDto,
  ProgressionConfigRequest,
  WeightUnit,
} from "./workoutSessionTypes";
import type { RepsPerSetConfig } from "./ExerciseSubstitutionModal";
import type { ExerciseConfigUpdate } from "./EditExerciseConfigModal";
import type { ExercisePerformanceRequest, ExerciseTemplate, ExerciseUpdateRequest } from "@/types/workout";
import type { PulledWorkoutData } from "@/services/hevySyncService";

const WORKOUT_PROGRESS_KEY = "workout_progress";

function saveWorkoutProgress(
  workoutId: string,
  dayNumber: number,
  weekNumber: number,
  exerciseEntries: ExerciseEntry[]
): void {
  const progress: SavedWorkoutProgress = {
    workoutId,
    dayNumber,
    weekNumber,
    savedAt: new Date().toISOString(),
    exercises: exerciseEntries.map((entry) => ({
      exerciseId: entry.exercise.id,
      sets: entry.sets.map((set) => ({
        setNumber: set.setNumber,
        weight: set.weight,
        reps: set.reps,
        isAmrap: set.isAmrap,
        completed: set.completed,
      })),
    })),
  };
  localStorage.setItem(WORKOUT_PROGRESS_KEY, JSON.stringify(progress));
}

function loadWorkoutProgress(): SavedWorkoutProgress | null {
  try {
    const stored = localStorage.getItem(WORKOUT_PROGRESS_KEY);
    if (!stored) return null;
    return JSON.parse(stored) as SavedWorkoutProgress;
  } catch (error) {
    console.warn('Failed to parse saved workout progress:', error);
    return null;
  }
}

function clearWorkoutProgress(): void {
  localStorage.removeItem(WORKOUT_PROGRESS_KEY);
}

export function useWorkoutSession() {
  const { day } = useParams<{ day: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { data: workout, isLoading, refetch } = useCurrentWorkout();
  const substituteExercise = useSubstituteExercise();
  const updateExercisesMutation = useUpdateExercises();
  const undoCompletionMutation = useUndoCompletion();
  const removeExerciseMutation = useRemoveExercise();
  useHevy();
  const [exerciseEntries, setExerciseEntries] = useState<ExerciseEntry[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [completionResult, setCompletionResult] = useState<CompleteDayResult | null>(null);
  const [showCompletionSummary, setShowCompletionSummary] = useState(false);
  const [isPrefilled, setIsPrefilled] = useState(false);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);
  const [pendingSubstitutions, setPendingSubstitutions] = useState<DetectedSubstitution[]>([]);
  const [showUndoModal, setShowUndoModal] = useState(false);

  const locationState = location.state as {
    pulledData?: PulledWorkoutData[];
    pulledSubstitutions?: DetectedSubstitution[];
    weightDiscrepancies?: WeightDiscrepancy[];
    missingExercises?: MissingExercise[];
  } | null;
  const pulledData = locationState?.pulledData;
  const pulledSubstitutions = locationState?.pulledSubstitutions;
  const pulledWeightDiscrepancies = locationState?.weightDiscrepancies;
  const pulledMissingExercises = locationState?.missingExercises;

  const [substitutionModalOpen, setSubstitutionModalOpen] = useState(false);
  const [exerciseToSubstitute, setExerciseToSubstitute] = useState<ExerciseDto | null>(null);
  const [temporarySubstitutions, setTemporarySubstitutions] = useState<TemporarySubstitution[]>([]);
  const [exerciseToEdit, setExerciseToEdit] = useState<ExerciseDto | null>(null);

  const [weightDiscrepancies, setWeightDiscrepancies] = useState<WeightDiscrepancy[]>([]);
  const [showWeightDiscrepancyModal, setShowWeightDiscrepancyModal] = useState(false);
  const [missingExercises, setMissingExercises] = useState<MissingExercise[]>([]);
  const [showMissingExercisesModal, setShowMissingExercisesModal] = useState(false);
  const [missingExercisesProcessed, setMissingExercisesProcessed] = useState(false);
  const [showWeightConfirmationModal, setShowWeightConfirmationModal] = useState(false);
  const [pendingWeightExercises, setPendingWeightExercises] = useState<PendingWeightExerciseDto[]>([]);
  const [weightDiscrepanciesProcessed, setWeightDiscrepanciesProcessed] = useState(false);

  const [showRecoveryModal, setShowRecoveryModal] = useState(false);
  const [savedProgressData, setSavedProgressData] = useState<SavedWorkoutProgress | null>(null);
  const [progressRecovered, setProgressRecovered] = useState(false);

  const workoutStartTime = useRef<Date>(new Date());
  const workoutEndTime = useRef<Date>(new Date());

  const dayNumber = parseInt(day || "1") as DayNumber;
  const dayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  const dayExercises = useMemo(() => {
    if (!workout) return [];
    return workout.exercises
      .filter((e) => e.assignedDay === dayNumber)
      .sort((a, b) => a.orderInDay - b.orderInDay);
  }, [workout, dayNumber]);

  // Initialize exercise entries when workout loads
  useEffect(() => {
    if (dayExercises.length > 0 && exerciseEntries.length === 0 && workout) {
      const currentWeek = workout.currentWeek;
      const weekParams = getWeekParameters(currentWeek);

      const entries = dayExercises.map((exercise) => {
        const isLinear = exercise.progression.type === "Linear";
        const isRepsPerSet = exercise.progression.type === "RepsPerSet";
        const isMinimalSets = exercise.progression.type === "MinimalSets";

        let targetSets = 3;
        let targetReps = 10;
        let targetWeight = 50;
        let weightUnit = "kg";
        let isAmrapExercise = false;

        if (isLinear) {
          const prog = exercise.progression as LinearProgressionDto;
          targetSets = weekParams.sets;
          targetReps = weekParams.targetReps;
          targetWeight = roundToGymIncrement(prog.trainingMax.value * weekParams.intensity);
          weightUnit = prog.trainingMax.unit === 1 ? "kg" : "lbs";
          isAmrapExercise = prog.useAmrap;
        } else if (isRepsPerSet) {
          const prog = exercise.progression as RepsPerSetProgressionDto;
          targetSets = prog.currentSetCount;
          targetReps = prog.repRange.target;
          targetWeight = prog.isWeightPending ? 0 : prog.currentWeight;
          weightUnit = prog.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
        } else if (isMinimalSets) {
          const prog = exercise.progression as MinimalSetsProgressionDto;
          targetSets = prog.currentSetCount;
          targetReps = Math.ceil(prog.targetTotalReps / prog.currentSetCount);
          targetWeight = prog.currentWeight;
          weightUnit = prog.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg";
        }

        const sets: SetEntry[] = [];
        for (let i = 1; i <= targetSets; i++) {
          sets.push({
            setNumber: i,
            weight: Math.round(targetWeight * 10) / 10,
            reps: targetReps,
            isAmrap: isAmrapExercise && i === targetSets,
            completed: false,
          });
        }

        return { exercise, sets, targetSets, targetReps, targetWeight, weightUnit, isAmrapExercise };
      });
      setExerciseEntries(entries);
    }
  }, [dayExercises, exerciseEntries.length, workout]);

  const convertWeightFromKg = (weightKg: number, targetUnit: string) => {
    if (targetUnit === "lbs") {
      return Math.round(kgToLbs(weightKg) * 10) / 10;
    }
    return Math.round(weightKg * 10) / 10;
  };

  // Show substitution modal when substitutions are detected
  useEffect(() => {
    if (pulledSubstitutions && pulledSubstitutions.length > 0 && exerciseEntries.length > 0 && !showSubstitutionModal && pendingSubstitutions.length === 0 && !isPrefilled) {
      setPendingSubstitutions(pulledSubstitutions);
      setShowSubstitutionModal(true);
    }
  }, [pulledSubstitutions, exerciseEntries.length, showSubstitutionModal, pendingSubstitutions.length, isPrefilled]);

  // Show weight discrepancy modal after substitutions are handled
  useEffect(() => {
    if (pulledWeightDiscrepancies && pulledWeightDiscrepancies.length > 0 && !showSubstitutionModal && !showWeightDiscrepancyModal && weightDiscrepancies.length === 0 && !weightDiscrepanciesProcessed) {
      setWeightDiscrepancies(pulledWeightDiscrepancies);
      setShowWeightDiscrepancyModal(true);
    }
  }, [pulledWeightDiscrepancies, showSubstitutionModal, showWeightDiscrepancyModal, weightDiscrepancies.length, weightDiscrepanciesProcessed]);

  // Show missing exercises modal after weight discrepancies are handled
  useEffect(() => {
    if (pulledMissingExercises && pulledMissingExercises.length > 0 && !showWeightDiscrepancyModal && !showMissingExercisesModal && missingExercises.length === 0 && !missingExercisesProcessed) {
      setMissingExercises(pulledMissingExercises);
      setShowMissingExercisesModal(true);
    }
  }, [pulledMissingExercises, showWeightDiscrepancyModal, showMissingExercisesModal, missingExercises.length, missingExercisesProcessed]);

  // Prefill with pulled Hevy data
  useEffect(() => {
    if (pulledData && pulledData.length > 0 && exerciseEntries.length > 0 && !isPrefilled) {
      setExerciseEntries((prev) => {
        return prev.map((entry) => {
          const pulled = pulledData.find((p) => p.exerciseId === entry.exercise.id);
          if (pulled && pulled.sets.length > 0) {
            const newSets: SetEntry[] = pulled.sets.map((pulledSet, index) => ({
              setNumber: pulledSet.setNumber,
              weight: convertWeightFromKg(pulledSet.weight, entry.weightUnit),
              reps: pulledSet.reps,
              isAmrap: entry.isAmrapExercise && index === pulled.sets.length - 1,
              completed: true,
            }));
            return { ...entry, sets: newSets };
          }
          return entry;
        });
      });
      if (!pulledSubstitutions || pulledSubstitutions.length === 0) {
        setIsPrefilled(true);
        toast.success("Workout data prefilled from Hevy! Review and complete workout when ready.");
      }
    }
  }, [pulledData, exerciseEntries.length, isPrefilled, pulledSubstitutions]);

  // Check for saved progress on initial load
  useEffect(() => {
    if (!pulledData && !progressRecovered && exerciseEntries.length > 0 && workout) {
      const saved = loadWorkoutProgress();
      if (saved && saved.workoutId === workout.id && saved.dayNumber === dayNumber && saved.weekNumber === workout.currentWeek) {
        const hasCompletedSets = saved.exercises.some((ex) => ex.sets.some((set) => set.completed));
        if (hasCompletedSets) {
          setSavedProgressData(saved);
          setShowRecoveryModal(true);
        }
      }
      setProgressRecovered(true);
    }
  }, [pulledData, progressRecovered, exerciseEntries.length, workout, dayNumber]);

  // Auto-save progress to localStorage
  useEffect(() => {
    if (workout && exerciseEntries.length > 0 && !showCompletionSummary) {
      const hasCompletedSets = exerciseEntries.some((entry) => entry.sets.some((set) => set.completed));
      if (hasCompletedSets) {
        saveWorkoutProgress(workout.id, dayNumber, workout.currentWeek, exerciseEntries);
      }
    }
  }, [exerciseEntries, workout, dayNumber, showCompletionSummary]);

  const handleResumeProgress = () => {
    if (!savedProgressData) return;
    setExerciseEntries((prev) => {
      return prev.map((entry) => {
        const savedExercise = savedProgressData.exercises.find((ex) => ex.exerciseId === entry.exercise.id);
        if (savedExercise) {
          return {
            ...entry,
            sets: entry.sets.map((set, index) => {
              const savedSet = savedExercise.sets[index];
              if (savedSet) {
                return { ...set, weight: savedSet.weight, reps: savedSet.reps, completed: savedSet.completed };
              }
              return set;
            }),
          };
        }
        return entry;
      });
    });
    setShowRecoveryModal(false);
    toast.success("Progress restored!");
  };

  const handleStartFresh = () => {
    clearWorkoutProgress();
    setShowRecoveryModal(false);
    toast.success("Starting fresh workout");
  };

  const handleApplySubstitution = async (sub: DetectedSubstitution, isPermanent: boolean) => {
    const entryIndex = exerciseEntries.findIndex((e) => e.exercise.id === sub.originalExerciseId);
    if (entryIndex === -1) return;
    const entry = exerciseEntries[entryIndex];
    const newSets: SetEntry[] = sub.sets.map((pulledSet, index) => ({
      setNumber: pulledSet.setNumber,
      weight: convertWeightFromKg(pulledSet.weight, entry.weightUnit),
      reps: pulledSet.reps,
      isAmrap: entry.isAmrapExercise && index === sub.sets.length - 1,
      completed: true,
    }));

    if (isPermanent && workout) {
      try {
        await substituteExercise.mutateAsync({
          workoutId: workout.id,
          request: {
            exerciseId: sub.originalExerciseId,
            newExerciseName: sub.hevyExerciseName,
            newHevyExerciseTemplateId: sub.hevyTemplateId,
            reason: "Pulled from Hevy workout",
          },
        });
        setExerciseEntries((prev) =>
          prev.map((e, i) => i === entryIndex ? { ...e, exercise: { ...e.exercise, name: sub.hevyExerciseName }, sets: newSets } : e)
        );
        toast.success(`Permanently replaced "${sub.originalExerciseName}" with "${sub.hevyExerciseName}"`);
        await refetch();
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to substitute exercise";
        toast.error(message);
        return;
      }
    } else {
      setTemporarySubstitutions((prev) => [
        ...prev.filter((s) => s.originalExerciseId !== sub.originalExerciseId),
        { originalExerciseId: sub.originalExerciseId, originalName: sub.originalExerciseName, substituteName: sub.hevyExerciseName },
      ]);
      setExerciseEntries((prev) =>
        prev.map((e, i) => i === entryIndex ? { ...e, exercise: { ...e.exercise, name: sub.hevyExerciseName }, sets: newSets } : e)
      );
      toast.success(`Substituted "${sub.originalExerciseName}" with "${sub.hevyExerciseName}" for this session`);
    }
  };

  const handleRemoveFromSubstitution = async (sub: DetectedSubstitution) => {
    if (!workout) return;
    try {
      await removeExerciseMutation.mutateAsync({ workoutId: workout.id, exerciseId: sub.originalExerciseId });
      setExerciseEntries((prev) => prev.filter((e) => e.exercise.id !== sub.originalExerciseId));
      toast.success(`Removed "${sub.originalExerciseName}" from program`);
      await refetch();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to remove exercise";
      toast.error(message);
    }
  };

  const handleSubstitutionsComplete = () => {
    setShowSubstitutionModal(false);
    setIsPrefilled(true);
    toast.success("Workout data prefilled! Review and complete workout when ready.");
  };

  const handleApplyWeightDiscrepancy = async (discrepancy: WeightDiscrepancy, confirmedWeight: number, decision: 'skip' | 'update') => {
    setWeightDiscrepancies((prev) => prev.filter((d) => d.exerciseId !== discrepancy.exerciseId));

    if (decision === 'skip') {
      setExerciseEntries((prev) =>
        prev.map((entry) => entry.exercise.id === discrepancy.exerciseId ? { ...entry, skipProgression: true } : entry)
      );
      toast.success(`Will skip progression for "${discrepancy.exerciseName}" this week`);
    } else if (decision === 'update' && workout) {
      try {
        if (discrepancy.progressionType === 'Linear') {
          const weekParams = getWeekParameters(workout.currentWeek);
          const newTm = roundToGymIncrement(confirmedWeight / weekParams.intensity, 'kg');
          await updateExercisesMutation.mutateAsync({
            workoutId: workout.id,
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
          await refetch();
        } else {
          await workoutsApi.updateWorkingWeight(workout.id, discrepancy.exerciseId, confirmedWeight, 1, 'Updated from Hevy sync - weight discrepancy');
          toast.success(`Updated working weight for "${discrepancy.exerciseName}"`);
        }
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to update weight";
        toast.error(message);
        setWeightDiscrepancies((prev) => [...prev, discrepancy]);
        return;
      }
    }
  };

  const handleWeightDiscrepanciesComplete = () => {
    setWeightDiscrepanciesProcessed(true);
    setShowWeightDiscrepancyModal(false);
    toast.success("Weight changes applied!");
  };

  const handleMissingExercise = async (exercise: MissingExercise, decision: 'delete' | 'skip') => {
    if (decision === 'delete' || decision === 'skip') {
      setExerciseEntries((prev) =>
        prev.map((entry) => entry.exercise.id === exercise.exerciseId ? { ...entry, sets: [], skipProgression: true } : entry)
      );
    }
  };

  const handleMissingExercisesComplete = () => {
    setMissingExercisesProcessed(true);
    setShowMissingExercisesModal(false);
    setMissingExercises([]);
    setTimeout(() => { toast.success("Missing exercise decisions applied!"); }, 100);
  };

  const handleUndoCompletion = async () => {
    if (!workout) return;
    try {
      await undoCompletionMutation.mutateAsync(workout.id);
      toast.success("Workout undone successfully!");
      navigate(`/workout/day/${workout.currentDay}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to undo workout";
      toast.error(message);
      throw error;
    }
  };

  const handleSetChange = (exerciseIndex: number, setIndex: number, field: "weight" | "reps", value: number) => {
    setExerciseEntries((prev) => {
      const updated = [...prev];
      updated[exerciseIndex] = {
        ...updated[exerciseIndex],
        sets: updated[exerciseIndex].sets.map((set, idx) => idx === setIndex ? { ...set, [field]: value } : set),
      };
      return updated;
    });
  };

  const handleSetComplete = (exerciseIndex: number, setIndex: number) => {
    setExerciseEntries((prev) => {
      const updated = [...prev];
      updated[exerciseIndex] = {
        ...updated[exerciseIndex],
        sets: updated[exerciseIndex].sets.map((set, idx) => idx === setIndex ? { ...set, completed: !set.completed } : set),
      };
      return updated;
    });
  };

  const handleCompleteWorkout = async () => {
    if (!workout) return;
    setIsSubmitting(true);
    try {
      const performances: ExercisePerformanceRequest[] = exerciseEntries
        .map((entry) => {
          const isTemporarySubstitution = temporarySubstitutions.some((s) => s.originalExerciseId === entry.exercise.id);
          return {
            exerciseId: entry.exercise.id,
            completedSets: entry.sets.filter((set) => set.completed).map((set) => ({
              setNumber: set.setNumber,
              weight: set.weight,
              weightUnit: (entry.weightUnit === "kg" ? 1 : 2) as WeightUnit,
              actualReps: set.reps,
              wasAmrap: set.isAmrap,
            })),
            wasTemporarySubstitution: isTemporarySubstitution,
          };
        })
        .filter((perf) => perf.completedSets.length > 0);

      const result = await workoutsApi.completeDay(workout.id, dayNumber, { performances });
      workoutEndTime.current = new Date();
      setCompletionResult(result);
      if (result.exercisesPendingWeightConfirmation?.length > 0) {
        setPendingWeightExercises(result.exercisesPendingWeightConfirmation);
        setShowWeightConfirmationModal(true);
      } else {
        setShowCompletionSummary(true);
      }
      clearWorkoutProgress();
      await refetch();
    } catch (error) {
      console.error("Failed to complete workout:", error);
      let errorMessage = "Failed to complete workout. Please try again.";
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as { response?: { data?: { error?: string } } };
        if (axiosError.response?.data?.error) {
          errorMessage = axiosError.response.data.error;
        }
      }
      toast.error(errorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmWeights = async (confirmedWeights: { exerciseId: string; weight: number; unit: 1 | 2 }[]) => {
    if (!workout) return;
    try {
      for (const cw of confirmedWeights) {
        await workoutsApi.confirmStartingWeight(workout.id, cw.exerciseId, cw.weight, cw.unit);
      }
      toast.success("Starting weights confirmed!");
      setShowWeightConfirmationModal(false);
      setPendingWeightExercises([]);
      setShowCompletionSummary(true);
      await refetch();
    } catch (error) {
      console.error("Failed to confirm starting weights:", error);
      toast.error("Failed to confirm starting weights. Please try again.");
    }
  };

  const handleOpenSubstitution = (exercise: ExerciseDto) => {
    setExerciseToSubstitute(exercise);
    setSubstitutionModalOpen(true);
  };

  const handleTemporarySubstitute = (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    setTemporarySubstitutions((prev) => [
      ...prev.filter((s) => s.originalExerciseId !== originalExercise.id),
      { originalExerciseId: originalExercise.id, originalName: originalExercise.name, substituteName: substituteTemplate.name },
    ]);
    setExerciseEntries((prev) =>
      prev.map((entry) => {
        if (entry.exercise.id !== originalExercise.id) return entry;
        if (repsConfig) {
          const newSets: SetEntry[] = [];
          for (let i = 1; i <= repsConfig.sets; i++) {
            newSets.push({ setNumber: i, weight: repsConfig.startingWeight, reps: repsConfig.targetReps, isAmrap: false, completed: false });
          }
          return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name }, sets: newSets, targetSets: repsConfig.sets, targetReps: repsConfig.targetReps, targetWeight: repsConfig.startingWeight, isAmrapExercise: false };
        }
        return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name } };
      })
    );
    const message = repsConfig
      ? `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" (Reps Per Set: ${repsConfig.sets}×${repsConfig.targetReps})`
      : `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" for this session`;
    toast.success(message);
  };

  const handlePermanentSubstitute = async (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    if (!workout) return;
    try {
      await substituteExercise.mutateAsync({
        workoutId: workout.id,
        request: {
          exerciseId: originalExercise.id,
          newExerciseName: substituteTemplate.name,
          reason: repsConfig ? `User substitution - switched to RepsPerSet (${repsConfig.sets}×${repsConfig.minReps}-${repsConfig.targetReps}-${repsConfig.maxReps})` : "User substitution",
          newProgressionConfig: repsConfig ? { type: "RepsPerSet", repRangeMinimum: repsConfig.minReps, repRangeTarget: repsConfig.targetReps, repRangeMaximum: repsConfig.maxReps, startingWeight: repsConfig.startingWeight, weightUnit: 1, targetSets: repsConfig.sets } : undefined,
        },
      });
      setExerciseEntries((prev) =>
        prev.map((entry) => {
          if (entry.exercise.id !== originalExercise.id) return entry;
          if (repsConfig) {
            const newSets: SetEntry[] = [];
            for (let i = 1; i <= repsConfig.sets; i++) {
              newSets.push({ setNumber: i, weight: repsConfig.startingWeight, reps: repsConfig.targetReps, isAmrap: false, completed: false });
            }
            return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name }, sets: newSets, targetSets: repsConfig.sets, targetReps: repsConfig.targetReps, targetWeight: repsConfig.startingWeight, isAmrapExercise: false };
          }
          return { ...entry, exercise: { ...entry.exercise, name: substituteTemplate.name } };
        })
      );
      const message = repsConfig
        ? `Permanently replaced "${originalExercise.name}" with "${substituteTemplate.name}" (Reps Per Set progression)`
        : `Permanently replaced "${originalExercise.name}" with "${substituteTemplate.name}"`;
      toast.success(message);
      await refetch();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to substitute exercise";
      toast.error(message);
    }
  };

  const syncRoutineAfterChange = async (toastId: string, successMsg: string, failureMsg: string) => {
    const syncKey = `week${workout!.currentWeek}-day${dayNumber}`;
    const existingRoutineId = workout!.hevySyncedRoutines?.[syncKey];
    if (existingRoutineId) {
      try { await hevyApi.deleteRoutine(existingRoutineId); } catch (error) { console.warn('Failed to delete Hevy routine:', error); }
    }
    let folderId = workout!.hevyRoutineFolderId;
    if (!folderId) {
      const folderResult = await getOrCreateRoutineFolder(workout!.name);
      if (folderResult) {
        folderId = folderResult.folderId;
        try { await workoutsApi.setHevyFolderId(workout!.id, folderId); } catch (error) { console.warn('Failed to set Hevy folder ID:', error); }
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
  };

  const handleToggleUnilateral = async (exercise: ExerciseDto) => {
    if (!workout) return;
    if (exercise.progression.type !== "RepsPerSet") {
      toast.error("Unilateral toggle only applies to RepsPerSet exercises");
      return;
    }
    const repsPerSetProg = exercise.progression as RepsPerSetProgressionDto;
    const newUnilateral = !repsPerSetProg.isUnilateral;

    try {
      toast.loading("Updating exercise...", { id: "toggle-unilateral" });
      await updateExercisesMutation.mutateAsync({
        workoutId: workout.id,
        request: { updates: [{ exerciseId: exercise.id, isUnilateral: newUnilateral, reason: `Set ${newUnilateral ? "unilateral" : "bilateral"} mode` }] },
      });

      if (hevyApi.isConfigured()) {
        await syncRoutineAfterChange(
          "toggle-unilateral",
          `${exercise.name} is now ${newUnilateral ? "unilateral (per side)" : "bilateral"}. Hevy routine updated!`,
          "Exercise updated but Hevy sync failed"
        );
      } else {
        toast.success(`${exercise.name} is now ${newUnilateral ? "unilateral (per side)" : "bilateral"}`, { id: "toggle-unilateral" });
        await refetch();
      }

      setExerciseEntries((prev) =>
        prev.map((entry) => {
          if (entry.exercise.id !== exercise.id) return entry;
          const currentSetCount = entry.sets.length;
          let newSets: SetEntry[];
          if (newUnilateral) {
            newSets = [];
            for (let i = 0; i < currentSetCount * 2; i++) {
              const sourceSet = entry.sets[Math.floor(i / 2)];
              newSets.push({ setNumber: i + 1, weight: sourceSet.weight, reps: sourceSet.reps, isAmrap: false, completed: false });
            }
          } else {
            const newSetCount = Math.ceil(currentSetCount / 2);
            newSets = entry.sets.slice(0, newSetCount).map((set, i) => ({ ...set, setNumber: i + 1 }));
          }
          return { ...entry, sets: newSets, targetSets: newSets.length };
        })
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to toggle unilateral";
      toast.error(message, { id: "toggle-unilateral" });
    }
  };

  const handleSaveExerciseConfig = async (exerciseId: string, config: ExerciseConfigUpdate) => {
    if (!workout) return;
    try {
      toast.loading("Saving changes...", { id: "save-exercise-config" });
      const updateRequest: ExerciseUpdateRequest = { exerciseId, reason: "Manual configuration update" };
      if (config.trainingMaxValue !== undefined) { updateRequest.trainingMaxValue = config.trainingMaxValue; updateRequest.trainingMaxUnit = config.trainingMaxUnit; }
      if (config.weightValue !== undefined) { updateRequest.weightValue = config.weightValue; updateRequest.weightUnit = config.weightUnit; }
      if (config.isUnilateral !== undefined) { updateRequest.isUnilateral = config.isUnilateral; }

      await updateExercisesMutation.mutateAsync({ workoutId: workout.id, request: { updates: [updateRequest] } });

      if (hevyApi.isConfigured()) {
        await syncRoutineAfterChange(
          "save-exercise-config",
          "Exercise updated and Hevy routine refreshed!",
          "Exercise updated but Hevy sync failed"
        );
      } else {
        toast.success("Exercise configuration saved!", { id: "save-exercise-config" });
        await refetch();
      }

      const { data: refreshedWorkout } = await refetch();
      if (refreshedWorkout) {
        const updatedExercise = refreshedWorkout.exercises.find(e => e.id === exerciseId);
        if (updatedExercise) {
          setExerciseEntries(prev => prev.map(entry => {
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

  const handleChangeProgression = async (exerciseId: string, config: ProgressionConfigRequest) => {
    if (!workout) return;
    const exercise = workout.exercises.find(e => e.id === exerciseId);
    if (!exercise) return;
    try {
      toast.loading("Changing progression...", { id: "change-progression" });
      await substituteExercise.mutateAsync({
        workoutId: workout.id,
        request: { exerciseId, newExerciseName: exercise.name, reason: `Changed progression from ${exercise.progression.type} to ${config.type}`, newProgressionConfig: config },
      });

      if (hevyApi.isConfigured()) {
        await syncRoutineAfterChange(
          "change-progression",
          `Changed ${exercise.name} to ${config.type} progression. Hevy routine updated!`,
          "Progression changed but Hevy sync failed"
        );
      } else {
        toast.success(`Changed ${exercise.name} to ${config.type} progression`, { id: "change-progression" });
      }

      const { data: refreshedWorkout } = await refetch();
      if (refreshedWorkout) {
        const updatedExercise = refreshedWorkout.exercises.find(e => e.id === exerciseId);
        if (updatedExercise) {
          setExerciseEntries(prev => prev.map(entry => {
            if (entry.exercise.id !== exerciseId) return entry;
            const isLinear = updatedExercise.progression.type === "Linear";
            const isRepsPerSet = updatedExercise.progression.type === "RepsPerSet";
            const linearProg = isLinear ? (updatedExercise.progression as LinearProgressionDto) : null;
            const rpsProgression = isRepsPerSet ? (updatedExercise.progression as RepsPerSetProgressionDto) : null;
            let newSets: SetEntry[];
            let newTargetSets: number, newTargetReps: number, newTargetWeight: number, newIsAmrap: boolean;

            if (isLinear && linearProg) {
              const weekParams = getWeekParameters(workout.currentWeek);
              newTargetSets = weekParams.sets;
              newTargetReps = weekParams.targetReps;
              newTargetWeight = Math.round((linearProg.trainingMax.value * weekParams.intensity / 100) / 2.5) * 2.5;
              newIsAmrap = linearProg.useAmrap;
              newSets = Array.from({ length: newTargetSets }, (_, i) => ({ setNumber: i + 1, weight: newTargetWeight, reps: newTargetReps, isAmrap: newIsAmrap && i === newTargetSets - 1, completed: false }));
            } else if (isRepsPerSet && rpsProgression) {
              newTargetSets = rpsProgression.currentSetCount;
              newTargetReps = rpsProgression.repRange.target;
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

  const allSetsCompleted = exerciseEntries.every((entry) => entry.sets.every((set) => set.completed));
  const completedSetsCount = exerciseEntries.reduce((acc, entry) => acc + entry.sets.filter((s) => s.completed).length, 0);
  const totalSetsCount = exerciseEntries.reduce((acc, entry) => acc + entry.sets.length, 0);
  const completedExercisesCount = exerciseEntries.filter((entry) => entry.sets.length > 0 && entry.sets.every((set) => set.completed)).length;
  const totalExercisesCount = exerciseEntries.filter((entry) => entry.sets.length > 0).length;
  const progressPercentage = totalSetsCount > 0 ? (completedSetsCount / totalSetsCount) * 100 : 0;

  return {
    // State
    workout, isLoading, exerciseEntries, isSubmitting, completionResult, showCompletionSummary,
    isPrefilled, showSubstitutionModal, pendingSubstitutions, showUndoModal, showWeightConfirmationModal, pendingWeightExercises,
    substitutionModalOpen, exerciseToSubstitute, temporarySubstitutions, exerciseToEdit,
    weightDiscrepancies, showWeightDiscrepancyModal, missingExercises, showMissingExercisesModal,
    showRecoveryModal, savedProgressData,
    dayNumber, dayName, workoutStartTime, workoutEndTime,
    // Computed
    allSetsCompleted, completedSetsCount, totalSetsCount, completedExercisesCount, totalExercisesCount, progressPercentage,
    // Handlers
    handleSetChange, handleSetComplete, handleCompleteWorkout, handleOpenSubstitution,
    handleTemporarySubstitute, handlePermanentSubstitute, handleToggleUnilateral,
    handleSaveExerciseConfig, handleChangeProgression, handleUndoCompletion,
    handleApplySubstitution, handleRemoveFromSubstitution, handleSubstitutionsComplete,
    handleConfirmWeights, handleApplyWeightDiscrepancy, handleWeightDiscrepanciesComplete,
    handleMissingExercise, handleMissingExercisesComplete,
    handleResumeProgress, handleStartFresh,
    // Setters for modals
    setShowUndoModal, setSubstitutionModalOpen, setExerciseToSubstitute, setExerciseToEdit, setShowWeightConfirmationModal, setShowCompletionSummary,
    navigate, refetch,
  };
}
