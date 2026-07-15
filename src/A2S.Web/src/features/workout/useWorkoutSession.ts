import { useState, useMemo, useRef, useEffect } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import { useCurrentWorkout, useSubstituteExercise, useUpdateExercises, useUndoCompletion, useRemoveExercise } from "@/hooks/useWorkouts";
import { workoutsApi } from "@/api/workouts";
import { useHevy } from "@/contexts/HevyContext";
import { getWeekParameters, roundToGymIncrement } from "@/utils/weekParameters";
import { kgToLbs } from "@/utils/constants";
import toast from "react-hot-toast";
import { saveWorkoutProgress, loadWorkoutProgress, clearWorkoutProgress } from "./workoutProgressPersistence";
import {
  createTemporarySubstituteHandler,
  createPermanentSubstituteHandler,
  createSaveExerciseConfigHandler,
  createChangeProgressionHandler,
} from "./workoutConfigHandlers";
import {
  createApplySubstitutionHandler,
  createRemoveFromSubstitutionHandler,
  createApplyWeightDiscrepancyHandler,
  createMissingExerciseHandler,
} from "./workoutReconciliationHandlers";
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
  WeightUnit,
} from "./workoutSessionTypes";
import type { ExercisePerformanceRequest } from "@/types/workout";
import type { PulledWorkoutData } from "@/services/hevySyncService";

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
  const [weightConfirmationPhase, setWeightConfirmationPhase] = useState<"pre-completion" | "post-completion">("post-completion");
  const [weightDiscrepanciesProcessed, setWeightDiscrepanciesProcessed] = useState(false);

  const [showRecoveryModal, setShowRecoveryModal] = useState(false);
  const [savedProgressData, setSavedProgressData] = useState<SavedWorkoutProgress | null>(null);
  const [progressRecovered, setProgressRecovered] = useState(false);

  const workoutStartTime = useRef<Date>(new Date());
  const workoutEndTime = useRef<Date>(new Date());
  // Survives a failed submit so the user isn't re-asked to confirm weights the
  // backend has already accepted (re-confirming would be rejected).
  const startingWeightsConfirmed = useRef(false);

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
          targetReps = prog.repRange.maximum;
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

  const reconciliationDeps = { workout, exerciseEntries, setExerciseEntries, setTemporarySubstitutions, setIsPrefilled, substituteExercise, updateExercisesMutation, removeExerciseMutation, refetch, convertWeightFromKg };
  const handleApplySubstitution = createApplySubstitutionHandler(reconciliationDeps);
  const handleRemoveFromSubstitution = createRemoveFromSubstitutionHandler(reconciliationDeps);
  const handleApplyWeightDiscrepancy = async (discrepancy: WeightDiscrepancy, confirmedWeight: number, decision: 'skip' | 'update') => {
    setWeightDiscrepancies((prev) => prev.filter((d) => d.exerciseId !== discrepancy.exerciseId));
    const handler = createApplyWeightDiscrepancyHandler(reconciliationDeps);
    try {
      await handler(discrepancy, confirmedWeight, decision);
    } catch {
      setWeightDiscrepancies((prev) => [...prev, discrepancy]);
    }
  };
  const handleMissingExercise = createMissingExerciseHandler(reconciliationDeps);

  const handleSubstitutionsComplete = () => {
    setShowSubstitutionModal(false);
    setIsPrefilled(true);
    toast.success("Workout data prefilled! Review and complete workout when ready.");
  };

  const handleWeightDiscrepanciesComplete = () => {
    setWeightDiscrepanciesProcessed(true);
    setShowWeightDiscrepancyModal(false);
    toast.success("Weight changes applied!");
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

  // Exercises whose starting weight is still unconfirmed must be confirmed BEFORE
  // the day is submitted, so backing out of the modal cancels the completion
  // instead of leaving a completed day behind.
  const getUnconfirmedStartingWeightExercises = (): PendingWeightExerciseDto[] => {
    if (startingWeightsConfirmed.current) return [];
    return exerciseEntries
      .filter((entry) => {
        const prog = entry.exercise.progression;
        const isPending = prog.type === "RepsPerSet" && (prog as RepsPerSetProgressionDto).isWeightPending;
        return isPending && entry.sets.some((set) => set.completed);
      })
      .map((entry) => {
        const firstCompleted = entry.sets.find((set) => set.completed)!;
        return {
          exerciseId: entry.exercise.id,
          exerciseName: entry.exercise.name,
          suggestedWeight: firstCompleted.weight,
          weightUnit: entry.weightUnit === "lbs" ? "Pounds" : "Kilograms",
          confirmationType: "StartingWeight" as const,
        };
      });
  };

  const handleCompleteWorkout = async () => {
    if (!workout) return;
    const unconfirmed = getUnconfirmedStartingWeightExercises();
    if (unconfirmed.length > 0) {
      setPendingWeightExercises(unconfirmed);
      setWeightConfirmationPhase("pre-completion");
      setShowWeightConfirmationModal(true);
      return;
    }
    await submitCompletion();
  };

  const submitCompletion = async () => {
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
        setWeightConfirmationPhase("post-completion");
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
        const pending = pendingWeightExercises.find((p) => p.exerciseId === cw.exerciseId);
        if (pending?.confirmationType === "WorkingWeight") {
          await workoutsApi.confirmWorkingWeight(workout.id, cw.exerciseId, cw.weight, cw.unit);
        } else {
          await workoutsApi.confirmStartingWeight(workout.id, cw.exerciseId, cw.weight, cw.unit);
        }
      }
      setShowWeightConfirmationModal(false);
      setPendingWeightExercises([]);
      if (weightConfirmationPhase === "pre-completion") {
        startingWeightsConfirmed.current = true;
        toast.success("Starting weights confirmed!");
        await submitCompletion();
      } else {
        toast.success("Weights confirmed!");
        setShowCompletionSummary(true);
        await refetch();
      }
    } catch (error) {
      console.error("Failed to confirm weights:", error);
      toast.error("Failed to confirm weights. Please try again.");
    }
  };

  const handleSkipWeightConfirmation = () => {
    setShowWeightConfirmationModal(false);
    setPendingWeightExercises([]);
    if (weightConfirmationPhase === "pre-completion") {
      // Backing out before confirming means the day was never submitted.
      toast("Workout not completed yet — confirm starting weights to finish.");
    } else {
      setShowCompletionSummary(true);
    }
  };

  const handleOpenSubstitution = (exercise: ExerciseDto) => {
    setExerciseToSubstitute(exercise);
    setSubstitutionModalOpen(true);
  };

  const configDeps = { workout, dayNumber, refetch, setExerciseEntries, setTemporarySubstitutions, substituteExercise, updateExercisesMutation };
  const handleTemporarySubstitute = createTemporarySubstituteHandler(configDeps);
  const handlePermanentSubstitute = createPermanentSubstituteHandler(configDeps);
  const handleSaveExerciseConfig = createSaveExerciseConfigHandler(configDeps);
  const handleChangeProgression = createChangeProgressionHandler(configDeps);

  const allSetsCompleted = exerciseEntries.every((entry) => entry.sets.every((set) => set.completed));
  const completedSetsCount = exerciseEntries.reduce((acc, entry) => acc + entry.sets.filter((s) => s.completed).length, 0);
  const totalSetsCount = exerciseEntries.reduce((acc, entry) => acc + entry.sets.length, 0);
  const completedExercisesCount = exerciseEntries.filter((entry) => entry.sets.length > 0 && entry.sets.every((set) => set.completed)).length;
  const totalExercisesCount = exerciseEntries.filter((entry) => entry.sets.length > 0).length;
  const progressPercentage = totalSetsCount > 0 ? (completedSetsCount / totalSetsCount) * 100 : 0;

  return {
    // State
    workout, isLoading, exerciseEntries, isSubmitting, completionResult, showCompletionSummary,
    isPrefilled, showSubstitutionModal, pendingSubstitutions, showUndoModal, showWeightConfirmationModal, pendingWeightExercises, weightConfirmationPhase,
    substitutionModalOpen, exerciseToSubstitute, temporarySubstitutions, exerciseToEdit,
    weightDiscrepancies, showWeightDiscrepancyModal, missingExercises, showMissingExercisesModal,
    showRecoveryModal, savedProgressData,
    dayNumber, dayName, workoutStartTime, workoutEndTime,
    // Computed
    allSetsCompleted, completedSetsCount, totalSetsCount, completedExercisesCount, totalExercisesCount, progressPercentage,
    // Handlers
    handleSetChange, handleSetComplete, handleCompleteWorkout, handleOpenSubstitution,
    handleTemporarySubstitute, handlePermanentSubstitute,
    handleSaveExerciseConfig, handleChangeProgression, handleUndoCompletion,
    handleApplySubstitution, handleRemoveFromSubstitution, handleSubstitutionsComplete,
    handleConfirmWeights, handleSkipWeightConfirmation, handleApplyWeightDiscrepancy, handleWeightDiscrepanciesComplete,
    handleMissingExercise, handleMissingExercisesComplete,
    handleResumeProgress, handleStartFresh,
    // Setters for modals
    setShowUndoModal, setSubstitutionModalOpen, setExerciseToSubstitute, setExerciseToEdit, setShowWeightConfirmationModal, setShowCompletionSummary,
    navigate, refetch,
  };
}
