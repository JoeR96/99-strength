import { useState, useMemo, useRef, useEffect } from "react";
import { useParams, useNavigate, Link, useLocation } from "react-router-dom";
import { useCurrentWorkout, useSubstituteExercise, useUpdateExercises, useUndoCompletion, useRemoveExercise } from "@/hooks/useWorkouts";
import { UndoConfirmationModal } from "@/components/shared/UndoConfirmationModal";
import { workoutsApi } from "@/api/workouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Navbar } from "@/components/layout/Navbar";
import { useHevy } from "@/contexts/HevyContext";
import { createCompletedWorkoutInHevy, handleRoutineLifecycle, syncDayAsRoutine, getOrCreateRoutineFolder, type CompletedExerciseData } from "@/services/hevySyncService";
import { hevyApi } from "@/services/hevyApi";
import { ExerciseSubstitutionModal, type RepsPerSetConfig } from "./ExerciseSubstitutionModal";
import { EditExerciseConfigModal, type ExerciseConfigUpdate } from "./EditExerciseConfigModal";

import { getWeekParameters, roundToGymIncrement } from "@/utils/weekParameters";
import toast from "react-hot-toast";
import type {
  ExerciseDto,
  ExerciseTemplate,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  ExercisePerformanceRequest,
  CompleteDayResult,
  WeightUnit,
  DayNumber,
  WorkoutDto,
  ProgressionConfigRequest,
} from "@/types/workout";
import type { PulledWorkoutData, DetectedSubstitution, WeightDiscrepancy, MissingExercise } from "@/services/hevySyncService";

interface SetEntry {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  completed: boolean;
}

// Track temporary substitutions for this session only
interface TemporarySubstitution {
  originalExerciseId: string;
  originalName: string;
  substituteName: string;
}

interface ExerciseEntry {
  exercise: ExerciseDto;
  sets: SetEntry[];
  targetSets: number;
  targetReps: number;
  targetWeight: number;
  weightUnit: string;
  isAmrapExercise: boolean;
}

// LocalStorage workout progress types
interface SavedSetProgress {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  completed: boolean;
}

interface SavedExerciseProgress {
  exerciseId: string;
  sets: SavedSetProgress[];
}

interface SavedWorkoutProgress {
  workoutId: string;
  dayNumber: number;
  weekNumber: number;
  savedAt: string;
  exercises: SavedExerciseProgress[];
}

// LocalStorage key for workout progress
const WORKOUT_PROGRESS_KEY = "workout_progress";

// Save workout progress to localStorage
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

// Load workout progress from localStorage
function loadWorkoutProgress(): SavedWorkoutProgress | null {
  try {
    const stored = localStorage.getItem(WORKOUT_PROGRESS_KEY);
    if (!stored) return null;
    return JSON.parse(stored) as SavedWorkoutProgress;
  } catch {
    return null;
  }
}

// Clear workout progress from localStorage
function clearWorkoutProgress(): void {
  localStorage.removeItem(WORKOUT_PROGRESS_KEY);
}

export function WorkoutSession() {
  const { day } = useParams<{ day: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { data: workout, isLoading, refetch } = useCurrentWorkout();
  const substituteExercise = useSubstituteExercise();
  const updateExercisesMutation = useUpdateExercises();
  const undoCompletionMutation = useUndoCompletion();
  const removeExerciseMutation = useRemoveExercise();
  const { isConfigured: hevyConfigured } = useHevy();
  const [exerciseEntries, setExerciseEntries] = useState<ExerciseEntry[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [completionResult, setCompletionResult] = useState<CompleteDayResult | null>(null);
  const [showCompletionSummary, setShowCompletionSummary] = useState(false);
  const [isPrefilled, setIsPrefilled] = useState(false);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);
  const [pendingSubstitutions, setPendingSubstitutions] = useState<DetectedSubstitution[]>([]);
  const [showUndoModal, setShowUndoModal] = useState(false);

  // Get pulled data from navigation state (if coming from "Pull Workout" button)
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

  // Exercise substitution state
  const [substitutionModalOpen, setSubstitutionModalOpen] = useState(false);
  const [exerciseToSubstitute, setExerciseToSubstitute] = useState<ExerciseDto | null>(null);
  const [temporarySubstitutions, setTemporarySubstitutions] = useState<TemporarySubstitution[]>([]);

  // Exercise edit state
  const [exerciseToEdit, setExerciseToEdit] = useState<ExerciseDto | null>(null);


  // Weight discrepancy and missing exercise state
  const [weightDiscrepancies, setWeightDiscrepancies] = useState<WeightDiscrepancy[]>([]);
  const [showWeightDiscrepancyModal, setShowWeightDiscrepancyModal] = useState(false);
  const [missingExercises, setMissingExercises] = useState<MissingExercise[]>([]);
  const [showMissingExercisesModal, setShowMissingExercisesModal] = useState(false);
  const [missingExercisesProcessed, setMissingExercisesProcessed] = useState(false);
  const [weightDiscrepanciesProcessed, setWeightDiscrepanciesProcessed] = useState(false);

  // Session recovery state
  const [showRecoveryModal, setShowRecoveryModal] = useState(false);
  const [savedProgressData, setSavedProgressData] = useState<SavedWorkoutProgress | null>(null);
  const [progressRecovered, setProgressRecovered] = useState(false);

  // Track workout timing for Hevy sync
  const workoutStartTime = useRef<Date>(new Date());
  const workoutEndTime = useRef<Date>(new Date());

  const dayNumber = parseInt(day || "1") as DayNumber;
  const dayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  // Get exercises for this day
  const dayExercises = useMemo(() => {
    if (!workout) return [];
    return workout.exercises
      .filter((e) => e.assignedDay === dayNumber)
      .sort((a, b) => a.orderInDay - b.orderInDay);
  }, [workout, dayNumber]);

  // Initialize exercise entries when workout loads
  // Uses week-specific parameters that match the backend's CalculatePlannedSets()
  useMemo(() => {
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
          // Use week-specific sets and reps from the A2S program
          // This matches the backend's LinearProgressionStrategy.CalculatePlannedSets()
          targetSets = weekParams.sets;
          targetReps = weekParams.targetReps;
          // Calculate working weight using week's intensity percentage
          targetWeight = roundToGymIncrement(prog.trainingMax.value * weekParams.intensity);
          weightUnit = prog.trainingMax.unit === 1 ? "kg" : "lbs";
          isAmrapExercise = prog.useAmrap;
        } else if (isRepsPerSet) {
          const prog = exercise.progression as RepsPerSetProgressionDto;
          targetSets = prog.currentSetCount;
          targetReps = prog.repRange.target;
          targetWeight = prog.currentWeight;
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

        return {
          exercise,
          sets,
          targetSets,
          targetReps,
          targetWeight,
          weightUnit,
          isAmrapExercise,
        };
      });
      setExerciseEntries(entries);
    }
  }, [dayExercises, exerciseEntries.length, workout]);

  // Helper to convert kg to the exercise's weight unit
  const convertWeightFromKg = (weightKg: number, targetUnit: string) => {
    if (targetUnit === "lbs") {
      return Math.round(weightKg / 0.453592 * 10) / 10;
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

  // Prefill with pulled Hevy data if available (for exact matches)
  useEffect(() => {
    if (pulledData && pulledData.length > 0 && exerciseEntries.length > 0 && !isPrefilled) {
      setExerciseEntries((prev) => {
        return prev.map((entry) => {
          // Find matching pulled data for this exercise
          const pulled = pulledData.find(
            (p) => p.exerciseId === entry.exercise.id
          );

          if (pulled && pulled.sets.length > 0) {
            // Create sets from pulled data, preserving isAmrap from our entries
            // Auto-mark as completed since data came from Hevy (user already did the workout)
            const newSets: SetEntry[] = pulled.sets.map((pulledSet, index) => ({
              setNumber: pulledSet.setNumber,
              weight: convertWeightFromKg(pulledSet.weight, entry.weightUnit),
              reps: pulledSet.reps,
              isAmrap: entry.isAmrapExercise && index === pulled.sets.length - 1,
              completed: true, // Auto-mark as completed since pulled from Hevy
            }));

            return {
              ...entry,
              sets: newSets,
            };
          }

          return entry;
        });
      });

      // Only mark as prefilled if there are no substitutions to handle
      if (!pulledSubstitutions || pulledSubstitutions.length === 0) {
        setIsPrefilled(true);
        toast.success("Workout data prefilled from Hevy! Review and complete workout when ready.");
      }
    }
  }, [pulledData, exerciseEntries.length, isPrefilled, pulledSubstitutions]);

  // Check for saved progress on initial load (only if no pulled data)
  useEffect(() => {
    if (!pulledData && !progressRecovered && exerciseEntries.length > 0 && workout) {
      const saved = loadWorkoutProgress();
      if (saved && saved.workoutId === workout.id && saved.dayNumber === dayNumber && saved.weekNumber === workout.currentWeek) {
        // Check if there's any completed set in the saved progress
        const hasCompletedSets = saved.exercises.some((ex) =>
          ex.sets.some((set) => set.completed)
        );
        if (hasCompletedSets) {
          setSavedProgressData(saved);
          setShowRecoveryModal(true);
        }
      }
      setProgressRecovered(true);
    }
  }, [pulledData, progressRecovered, exerciseEntries.length, workout, dayNumber]);

  // Auto-save progress to localStorage whenever exerciseEntries changes
  useEffect(() => {
    if (workout && exerciseEntries.length > 0 && !showCompletionSummary) {
      // Only save if there's at least one completed set
      const hasCompletedSets = exerciseEntries.some((entry) =>
        entry.sets.some((set) => set.completed)
      );
      if (hasCompletedSets) {
        saveWorkoutProgress(workout.id, dayNumber, workout.currentWeek, exerciseEntries);
      }
    }
  }, [exerciseEntries, workout, dayNumber, showCompletionSummary]);

  // Handle resuming saved progress
  const handleResumeProgress = () => {
    if (!savedProgressData) return;

    setExerciseEntries((prev) => {
      return prev.map((entry) => {
        const savedExercise = savedProgressData.exercises.find(
          (ex) => ex.exerciseId === entry.exercise.id
        );
        if (savedExercise) {
          return {
            ...entry,
            sets: entry.sets.map((set, index) => {
              const savedSet = savedExercise.sets[index];
              if (savedSet) {
                return {
                  ...set,
                  weight: savedSet.weight,
                  reps: savedSet.reps,
                  completed: savedSet.completed,
                };
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

  // Handle starting fresh (discard saved progress)
  const handleStartFresh = () => {
    clearWorkoutProgress();
    setShowRecoveryModal(false);
    toast.success("Starting fresh workout");
  };

  // Handle applying a substitution (temporary or permanent)
  const handleApplySubstitution = async (
    sub: DetectedSubstitution,
    isPermanent: boolean
  ) => {
    // Find the entry for this exercise
    const entryIndex = exerciseEntries.findIndex(
      (e) => e.exercise.id === sub.originalExerciseId
    );

    if (entryIndex === -1) return;

    const entry = exerciseEntries[entryIndex];

    // Apply the set data - auto-mark as completed since pulled from Hevy
    const newSets: SetEntry[] = sub.sets.map((pulledSet, index) => ({
      setNumber: pulledSet.setNumber,
      weight: convertWeightFromKg(pulledSet.weight, entry.weightUnit),
      reps: pulledSet.reps,
      isAmrap: entry.isAmrapExercise && index === sub.sets.length - 1,
      completed: true, // Auto-mark as completed since pulled from Hevy
    }));

    if (isPermanent && workout) {
      // Permanent substitution - update via API
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

        // Update local state with new name and sets
        setExerciseEntries((prev) =>
          prev.map((e, i) =>
            i === entryIndex
              ? {
                  ...e,
                  exercise: { ...e.exercise, name: sub.hevyExerciseName },
                  sets: newSets,
                }
              : e
          )
        );

        toast.success(`Permanently replaced "${sub.originalExerciseName}" with "${sub.hevyExerciseName}"`);
        await refetch();
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to substitute exercise";
        toast.error(message);
        return;
      }
    } else {
      // Temporary substitution - just update display name and sets
      setTemporarySubstitutions((prev) => [
        ...prev.filter((s) => s.originalExerciseId !== sub.originalExerciseId),
        {
          originalExerciseId: sub.originalExerciseId,
          originalName: sub.originalExerciseName,
          substituteName: sub.hevyExerciseName,
        },
      ]);

      setExerciseEntries((prev) =>
        prev.map((e, i) =>
          i === entryIndex
            ? {
                ...e,
                exercise: { ...e.exercise, name: sub.hevyExerciseName },
                sets: newSets,
              }
            : e
        )
      );

      toast.success(`Substituted "${sub.originalExerciseName}" with "${sub.hevyExerciseName}" for this session`);
    }
  };

  // Handle removing an exercise from the substitution modal
  const handleRemoveFromSubstitution = async (sub: DetectedSubstitution) => {
    if (!workout) return;
    try {
      await removeExerciseMutation.mutateAsync({
        workoutId: workout.id,
        exerciseId: sub.originalExerciseId,
      });
      // Remove from local exercise entries
      setExerciseEntries((prev) =>
        prev.filter((e) => e.exercise.id !== sub.originalExerciseId)
      );
      toast.success(`Removed "${sub.originalExerciseName}" from program`);
      await refetch();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to remove exercise";
      toast.error(message);
    }
  };

  // Handle completing all substitution decisions
  const handleSubstitutionsComplete = () => {
    setShowSubstitutionModal(false);
    setIsPrefilled(true);
    toast.success("Workout data prefilled! Review and complete workout when ready.");
  };

  // Handle applying a weight discrepancy decision
  const handleApplyWeightDiscrepancy = async (
    discrepancy: WeightDiscrepancy,
    confirmedWeight: number,
    decision: 'skip' | 'update'
  ) => {
    // Remove from pending discrepancies FIRST to prevent re-triggering
    setWeightDiscrepancies((prev) =>
      prev.filter((d) => d.exerciseId !== discrepancy.exerciseId)
    );

    if (decision === 'skip') {
      // Mark to skip progression for this week
      setExerciseEntries((prev) =>
        prev.map((entry) =>
          entry.exercise.id === discrepancy.exerciseId
            ? { ...entry, skipProgression: true }
            : entry
        )
      );
      toast.success(`Will skip progression for "${discrepancy.exerciseName}" this week`);
    } else if (decision === 'update' && workout) {
      try {
        if (discrepancy.progressionType === 'Linear') {
          // For Linear exercises, back-calculate and update the training max
          const weekParams = getWeekParameters(workout.currentWeek);
          const newTm = roundToGymIncrement(confirmedWeight / weekParams.intensity, 'kg');
          await updateExercisesMutation.mutateAsync({
            workoutId: workout.id,
            request: {
              updates: [{
                exerciseId: discrepancy.exerciseId,
                trainingMaxValue: newTm,
                trainingMaxUnit: 1, // Kilograms
                reason: `Updated TM from Hevy sync: actual weight ${confirmedWeight}kg at ${Math.round(weekParams.intensity * 100)}% intensity → TM ${newTm}kg`,
              }],
            },
          });
          toast.success(`Updated Training Max for "${discrepancy.exerciseName}" to ${newTm}kg`);
          await refetch();
        } else {
          // Update working weight directly (for RepsPerSet/MinimalSets)
          await workoutsApi.updateWorkingWeight(
            workout.id,
            discrepancy.exerciseId,
            confirmedWeight,
            1, // 1 = Kilograms (weight is already in kg from discrepancy)
            'Updated from Hevy sync - weight discrepancy'
          );
          toast.success(`Updated working weight for "${discrepancy.exerciseName}"`);
        }
      } catch (error) {
        const message = error instanceof Error ? error.message : "Failed to update weight";
        toast.error(message);
        // Re-add the discrepancy back if the update failed
        setWeightDiscrepancies((prev) => [...prev, discrepancy]);
        return;
      }
    }
  };

  // Handle completing all weight discrepancy decisions
  const handleWeightDiscrepanciesComplete = () => {
    // Mark as processed to prevent re-triggering
    setWeightDiscrepanciesProcessed(true);
    setShowWeightDiscrepancyModal(false);
    toast.success("Weight changes applied!");
  };

  // Handle applying a missing exercise decision
  const handleMissingExercise = async (
    exercise: MissingExercise,
    decision: 'delete' | 'skip'
  ) => {
    // Don't remove from list here - let the modal handle that after all decisions are applied
    // Just update the exercise entries to mark them appropriately

    if (decision === 'delete') {
      // Mark with empty sets and skip progression to remove from this session
      setExerciseEntries((prev) =>
        prev.map((entry) =>
          entry.exercise.id === exercise.exerciseId
            ? { ...entry, sets: [], skipProgression: true }
            : entry
        )
      );
    } else {
      // Skip this week - mark with zero sets to skip progression
      setExerciseEntries((prev) =>
        prev.map((entry) =>
          entry.exercise.id === exercise.exerciseId
            ? { ...entry, sets: [], skipProgression: true }
            : entry
        )
      );
    }
  };

  // Handle completing all missing exercise decisions
  const handleMissingExercisesComplete = () => {
    // Mark as processed to prevent re-triggering
    setMissingExercisesProcessed(true);
    // Close modal first
    setShowMissingExercisesModal(false);
    // Then clear the array
    setMissingExercises([]);
    // Show toast after state updates
    setTimeout(() => {
      toast.success("Missing exercise decisions applied!");
    }, 100);
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
      throw error; // Re-throw so modal knows it failed
    }
  };

  const handleSetChange = (
    exerciseIndex: number,
    setIndex: number,
    field: "weight" | "reps",
    value: number
  ) => {
    setExerciseEntries((prev) => {
      const updated = [...prev];
      updated[exerciseIndex] = {
        ...updated[exerciseIndex],
        sets: updated[exerciseIndex].sets.map((set, idx) =>
          idx === setIndex ? { ...set, [field]: value } : set
        ),
      };
      return updated;
    });
  };

  const handleSetComplete = (exerciseIndex: number, setIndex: number) => {
    setExerciseEntries((prev) => {
      const updated = [...prev];
      updated[exerciseIndex] = {
        ...updated[exerciseIndex],
        sets: updated[exerciseIndex].sets.map((set, idx) =>
          idx === setIndex ? { ...set, completed: !set.completed } : set
        ),
      };
      return updated;
    });
  };

  const handleCompleteWorkout = async () => {
    if (!workout) return;

    setIsSubmitting(true);
    try {
      // Build performances, filtering out exercises with no completed sets
      // (backend requires at least one completed set per exercise performance)
      const performances: ExercisePerformanceRequest[] = exerciseEntries
        .map((entry) => {
          // Check if this exercise was temporarily substituted
          const isTemporarySubstitution = temporarySubstitutions.some(
            (s) => s.originalExerciseId === entry.exercise.id
          );

          return {
            exerciseId: entry.exercise.id,
            completedSets: entry.sets
              .filter((set) => set.completed)
              .map((set) => ({
                setNumber: set.setNumber,
                weight: set.weight,
                weightUnit: (entry.weightUnit === "kg" ? 1 : 2) as WeightUnit,
                actualReps: set.reps,
                wasAmrap: set.isAmrap,
              })),
            // Skip progression for temporary substitutions
            wasTemporarySubstitution: isTemporarySubstitution,
          };
        })
        .filter((perf) => perf.completedSets.length > 0);

      const result = await workoutsApi.completeDay(workout.id, dayNumber, {
        performances,
      });

      workoutEndTime.current = new Date();
      setCompletionResult(result);
      setShowCompletionSummary(true);
      // Clear saved progress on successful completion
      clearWorkoutProgress();
      await refetch();
    } catch (error) {
      console.error("Failed to complete workout:", error);
      // Log the request for debugging
      console.error("Request details:", {
        workoutId: workout.id,
        dayNumber,
        currentWeek: workout.currentWeek,
        performances: exerciseEntries.map(e => ({
          exerciseName: e.exercise.name,
          exerciseId: e.exercise.id,
          progressionType: e.exercise.progression.type,
          completedSetsCount: e.sets.filter(s => s.completed).length,
          totalSetsShown: e.sets.length,
        }))
      });
      // Extract error message from axios error if available
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

  // Open substitution modal for an exercise
  const handleOpenSubstitution = (exercise: ExerciseDto) => {
    setExerciseToSubstitute(exercise);
    setSubstitutionModalOpen(true);
  };

  // Handle temporary substitution (this session only)
  const handleTemporarySubstitute = (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    // Add to temporary substitutions list
    setTemporarySubstitutions((prev) => [
      ...prev.filter((s) => s.originalExerciseId !== originalExercise.id),
      {
        originalExerciseId: originalExercise.id,
        originalName: originalExercise.name,
        substituteName: substituteTemplate.name,
      },
    ]);

    // Update the exercise entry with the new name and optionally new sets/reps config
    setExerciseEntries((prev) =>
      prev.map((entry) => {
        if (entry.exercise.id !== originalExercise.id) return entry;

        // If repsConfig is provided, rebuild the sets with new configuration
        if (repsConfig) {
          const newSets: SetEntry[] = [];
          for (let i = 1; i <= repsConfig.sets; i++) {
            newSets.push({
              setNumber: i,
              weight: repsConfig.startingWeight,
              reps: repsConfig.targetReps,
              isAmrap: false, // RepsPerSet doesn't use AMRAP
              completed: false,
            });
          }
          return {
            ...entry,
            exercise: {
              ...entry.exercise,
              name: substituteTemplate.name,
            },
            sets: newSets,
            targetSets: repsConfig.sets,
            targetReps: repsConfig.targetReps,
            targetWeight: repsConfig.startingWeight,
            isAmrapExercise: false,
          };
        }

        return {
          ...entry,
          exercise: {
            ...entry.exercise,
            name: substituteTemplate.name,
          },
        };
      })
    );

    const message = repsConfig
      ? `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" (Reps Per Set: ${repsConfig.sets}×${repsConfig.targetReps})`
      : `Substituted "${originalExercise.name}" with "${substituteTemplate.name}" for this session`;
    toast.success(message);
  };

  // Handle permanent substitution (entire workout)
  const handlePermanentSubstitute = async (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => {
    if (!workout) return;

    try {
      await substituteExercise.mutateAsync({
        workoutId: workout.id,
        request: {
          exerciseId: originalExercise.id,
          newExerciseName: substituteTemplate.name,
          reason: repsConfig
            ? `User substitution - switched to RepsPerSet (${repsConfig.sets}×${repsConfig.minReps}-${repsConfig.targetReps}-${repsConfig.maxReps})`
            : "User substitution",
          newProgressionConfig: repsConfig
            ? {
                type: "RepsPerSet",
                repRangeMinimum: repsConfig.minReps,
                repRangeTarget: repsConfig.targetReps,
                repRangeMaximum: repsConfig.maxReps,
                startingWeight: repsConfig.startingWeight,
                weightUnit: 1, // Kilograms
                targetSets: repsConfig.sets,
              }
            : undefined,
        },
      });

      // Update local state immediately, with new sets if repsConfig provided
      setExerciseEntries((prev) =>
        prev.map((entry) => {
          if (entry.exercise.id !== originalExercise.id) return entry;

          // If repsConfig is provided, rebuild the sets with new configuration
          if (repsConfig) {
            const newSets: SetEntry[] = [];
            for (let i = 1; i <= repsConfig.sets; i++) {
              newSets.push({
                setNumber: i,
                weight: repsConfig.startingWeight,
                reps: repsConfig.targetReps,
                isAmrap: false, // RepsPerSet doesn't use AMRAP
                completed: false,
              });
            }
            return {
              ...entry,
              exercise: {
                ...entry.exercise,
                name: substituteTemplate.name,
              },
              sets: newSets,
              targetSets: repsConfig.sets,
              targetReps: repsConfig.targetReps,
              targetWeight: repsConfig.startingWeight,
              isAmrapExercise: false,
            };
          }

          return {
            ...entry,
            exercise: {
              ...entry.exercise,
              name: substituteTemplate.name,
            },
          };
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

  // Handle toggling unilateral mode for RepsPerSet exercises
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

      // Step 1: Update the exercise in the backend
      await updateExercisesMutation.mutateAsync({
        workoutId: workout.id,
        request: {
          updates: [{
            exerciseId: exercise.id,
            isUnilateral: newUnilateral,
            reason: `Set ${newUnilateral ? "unilateral" : "bilateral"} mode`,
          }],
        },
      });

      // Step 2: If Hevy is configured, delete old routine and create new one
      if (hevyApi.isConfigured()) {
        const syncKey = `week${workout.currentWeek}-day${dayNumber}`;
        const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

        if (existingRoutineId) {
          // Delete the old routine
          try {
            await hevyApi.deleteRoutine(existingRoutineId);
            console.log(`Deleted old routine ${existingRoutineId}`);
          } catch (deleteError) {
            console.warn("Failed to delete old routine:", deleteError);
            // Continue anyway - might not exist anymore
          }
        }

        // Ensure folder exists
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

        // Refetch workout to get updated data before syncing
        const { data: updatedWorkout } = await refetch();

        if (updatedWorkout) {
          // Create new routine with updated sets
          const result = await syncDayAsRoutine(updatedWorkout, dayNumber, folderId, true);
          if (result.success) {
            toast.success(
              `${exercise.name} is now ${newUnilateral ? "unilateral (per side)" : "bilateral"}. Hevy routine updated!`,
              { id: "toggle-unilateral" }
            );
          } else {
            toast.error(`Exercise updated but Hevy sync failed: ${result.message}`, { id: "toggle-unilateral" });
          }
        }
      } else {
        toast.success(
          `${exercise.name} is now ${newUnilateral ? "unilateral (per side)" : "bilateral"}`,
          { id: "toggle-unilateral" }
        );
        await refetch();
      }

      // Update local state to double/halve sets
      setExerciseEntries((prev) =>
        prev.map((entry) => {
          if (entry.exercise.id !== exercise.id) return entry;

          const currentSetCount = entry.sets.length;
          let newSets: SetEntry[];

          if (newUnilateral) {
            // Switching to unilateral: double the sets
            newSets = [];
            for (let i = 0; i < currentSetCount * 2; i++) {
              const sourceSet = entry.sets[Math.floor(i / 2)];
              newSets.push({
                setNumber: i + 1,
                weight: sourceSet.weight,
                reps: sourceSet.reps,
                isAmrap: false,
                completed: false,
              });
            }
          } else {
            // Switching to bilateral: halve the sets
            const newSetCount = Math.ceil(currentSetCount / 2);
            newSets = entry.sets.slice(0, newSetCount).map((set, i) => ({
              ...set,
              setNumber: i + 1,
            }));
          }

          return {
            ...entry,
            sets: newSets,
            targetSets: newSets.length,
          };
        })
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to toggle unilateral";
      toast.error(message, { id: "toggle-unilateral" });
    }
  };

  // Handle saving exercise configuration changes
  const handleSaveExerciseConfig = async (exerciseId: string, config: ExerciseConfigUpdate) => {
    if (!workout) return;

    try {
      toast.loading("Saving changes...", { id: "save-exercise-config" });

      // Build the update request
      const updateRequest: {
        exerciseId: string;
        trainingMaxValue?: number;
        trainingMaxUnit?: number;
        weightValue?: number;
        weightUnit?: number;
        isUnilateral?: boolean;
        reason?: string;
      } = {
        exerciseId,
        reason: "Manual configuration update",
      };

      if (config.trainingMaxValue !== undefined) {
        updateRequest.trainingMaxValue = config.trainingMaxValue;
        updateRequest.trainingMaxUnit = config.trainingMaxUnit;
      }
      if (config.weightValue !== undefined) {
        updateRequest.weightValue = config.weightValue;
        updateRequest.weightUnit = config.weightUnit;
      }
      if (config.isUnilateral !== undefined) {
        updateRequest.isUnilateral = config.isUnilateral;
      }

      // Update the exercise in the backend
      await updateExercisesMutation.mutateAsync({
        workoutId: workout.id,
        request: {
          updates: [updateRequest],
        },
      });

      // If Hevy is configured, update the routine
      if (hevyApi.isConfigured()) {
        const syncKey = `week${workout.currentWeek}-day${dayNumber}`;
        const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

        if (existingRoutineId) {
          try {
            await hevyApi.deleteRoutine(existingRoutineId);
            console.log(`Deleted old routine ${existingRoutineId}`);
          } catch (deleteError) {
            console.warn("Failed to delete old routine:", deleteError);
          }
        }

        // Ensure folder exists
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

        // Refetch workout to get updated data before syncing
        const { data: updatedWorkout } = await refetch();

        if (updatedWorkout) {
          const result = await syncDayAsRoutine(updatedWorkout, dayNumber, folderId, true);
          if (result.success) {
            toast.success("Exercise updated and Hevy routine refreshed!", { id: "save-exercise-config" });
          } else {
            toast.error(`Exercise updated but Hevy sync failed: ${result.message}`, { id: "save-exercise-config" });
          }
        }
      } else {
        toast.success("Exercise configuration saved!", { id: "save-exercise-config" });
        await refetch();
      }

      // Update local state to reflect changes
      const { data: refreshedWorkout } = await refetch();
      if (refreshedWorkout) {
        // Re-initialize exercise entries with new data
        const updatedExercise = refreshedWorkout.exercises.find(e => e.id === exerciseId);
        if (updatedExercise) {
          setExerciseEntries(prev => prev.map(entry => {
            if (entry.exercise.id !== exerciseId) return entry;

            // Rebuild the entry with updated exercise data
            const isRepsPerSet = updatedExercise.progression.type === "RepsPerSet";
            const repsPerSetProg = isRepsPerSet
              ? (updatedExercise.progression as RepsPerSetProgressionDto)
              : null;

            // If unilateral changed, adjust sets
            const previousUnilateral = (entry.exercise.progression as RepsPerSetProgressionDto)?.isUnilateral;
            const newUnilateral = repsPerSetProg?.isUnilateral;

            if (isRepsPerSet && previousUnilateral !== newUnilateral) {
              const currentSetCount = entry.sets.length;
              let newSets: SetEntry[];

              if (newUnilateral && !previousUnilateral) {
                // Becoming unilateral: double sets
                newSets = [];
                for (let i = 0; i < currentSetCount * 2; i++) {
                  const sourceSet = entry.sets[Math.floor(i / 2)];
                  newSets.push({
                    setNumber: i + 1,
                    weight: config.weightValue ?? sourceSet.weight,
                    reps: sourceSet.reps,
                    isAmrap: false,
                    completed: false,
                  });
                }
              } else if (!newUnilateral && previousUnilateral) {
                // Becoming bilateral: halve sets
                const newSetCount = Math.ceil(currentSetCount / 2);
                newSets = entry.sets.slice(0, newSetCount).map((set, i) => ({
                  ...set,
                  setNumber: i + 1,
                  weight: config.weightValue ?? set.weight,
                }));
              } else {
                // Just update weight
                newSets = entry.sets.map(set => ({
                  ...set,
                  weight: config.weightValue ?? set.weight,
                }));
              }

              return {
                ...entry,
                exercise: updatedExercise,
                sets: newSets,
                targetSets: newSets.length,
                targetWeight: config.weightValue ?? entry.targetWeight,
              };
            }

            // For Linear or just weight change
            return {
              ...entry,
              exercise: updatedExercise,
              sets: entry.sets.map(set => ({
                ...set,
                weight: config.weightValue ?? config.trainingMaxValue ?? set.weight,
              })),
              targetWeight: config.weightValue ?? config.trainingMaxValue ?? entry.targetWeight,
            };
          }));
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to save changes";
      toast.error(message, { id: "save-exercise-config" });
      throw error;
    }
  };

  // Handle changing an exercise's progression type
  const handleChangeProgression = async (exerciseId: string, config: ProgressionConfigRequest) => {
    if (!workout) return;

    const exercise = workout.exercises.find(e => e.id === exerciseId);
    if (!exercise) return;

    try {
      toast.loading("Changing progression...", { id: "change-progression" });

      // Use the substitute API with the same exercise name but new progression config
      await substituteExercise.mutateAsync({
        workoutId: workout.id,
        request: {
          exerciseId,
          newExerciseName: exercise.name,
          reason: `Changed progression from ${exercise.progression.type} to ${config.type}`,
          newProgressionConfig: config,
        },
      });

      // If Hevy is configured, delete old routine and push updated one
      if (hevyApi.isConfigured()) {
        const syncKey = `week${workout.currentWeek}-day${dayNumber}`;
        const existingRoutineId = workout.hevySyncedRoutines?.[syncKey];

        if (existingRoutineId) {
          try {
            await hevyApi.deleteRoutine(existingRoutineId);
            console.log(`Deleted old routine ${existingRoutineId}`);
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

        const { data: updatedWorkout } = await refetch();
        if (updatedWorkout) {
          const result = await syncDayAsRoutine(updatedWorkout, dayNumber, folderId, true);
          if (result.success) {
            toast.success(
              `Changed ${exercise.name} to ${config.type} progression. Hevy routine updated!`,
              { id: "change-progression" }
            );
          } else {
            toast.error(
              `Progression changed but Hevy sync failed: ${result.message}`,
              { id: "change-progression" }
            );
          }
        }
      } else {
        toast.success(
          `Changed ${exercise.name} to ${config.type} progression`,
          { id: "change-progression" }
        );
      }

      // Refetch and rebuild exercise entries
      const { data: refreshedWorkout } = await refetch();
      if (refreshedWorkout) {
        const updatedExercise = refreshedWorkout.exercises.find(e => e.id === exerciseId);
        if (updatedExercise) {
          // Rebuild the entry with the new progression data
          setExerciseEntries(prev => prev.map(entry => {
            if (entry.exercise.id !== exerciseId) return entry;

            // Build new sets based on the new progression type
            const isLinear = updatedExercise.progression.type === "Linear";
            const isRepsPerSet = updatedExercise.progression.type === "RepsPerSet";
            const linearProg = isLinear ? (updatedExercise.progression as LinearProgressionDto) : null;
            const rpsProgression = isRepsPerSet ? (updatedExercise.progression as RepsPerSetProgressionDto) : null;

            let newSets: SetEntry[];
            let newTargetSets: number;
            let newTargetReps: number;
            let newTargetWeight: number;
            let newIsAmrap: boolean;

            if (isLinear && linearProg) {
              // Rebuild sets for Linear progression
              const weekParams = getWeekParameters(workout.currentWeek);
              newTargetSets = weekParams.sets;
              newTargetReps = weekParams.reps;
              newTargetWeight = Math.round((linearProg.trainingMax.value * weekParams.intensity / 100) / 2.5) * 2.5;
              newIsAmrap = linearProg.useAmrap;
              newSets = [];
              for (let i = 1; i <= newTargetSets; i++) {
                newSets.push({
                  setNumber: i,
                  weight: newTargetWeight,
                  reps: newTargetReps,
                  isAmrap: newIsAmrap && i === newTargetSets,
                  completed: false,
                });
              }
            } else if (isRepsPerSet && rpsProgression) {
              newTargetSets = rpsProgression.currentSetCount;
              newTargetReps = rpsProgression.repRange.target;
              newTargetWeight = rpsProgression.currentWeight;
              newIsAmrap = false;
              newSets = [];
              for (let i = 1; i <= newTargetSets; i++) {
                newSets.push({
                  setNumber: i,
                  weight: newTargetWeight,
                  reps: newTargetReps,
                  isAmrap: false,
                  completed: false,
                });
              }
            } else {
              // MinimalSets or fallback
              const minProg = updatedExercise.progression as MinimalSetsProgressionDto;
              newTargetSets = minProg?.currentSetCount ?? 4;
              newTargetReps = minProg ? Math.ceil(minProg.targetTotalReps / newTargetSets) : 10;
              newTargetWeight = minProg?.currentWeight ?? 0;
              newIsAmrap = false;
              newSets = [];
              for (let i = 1; i <= newTargetSets; i++) {
                newSets.push({
                  setNumber: i,
                  weight: newTargetWeight,
                  reps: newTargetReps,
                  isAmrap: false,
                  completed: false,
                });
              }
            }

            return {
              ...entry,
              exercise: updatedExercise,
              sets: newSets,
              targetSets: newTargetSets,
              targetReps: newTargetReps,
              targetWeight: newTargetWeight,
              isAmrapExercise: newIsAmrap,
              weightUnit: rpsProgression?.weightUnit ?? linearProg?.trainingMax.unit === 2 ? "Pounds" : "Kilograms",
            };
          }));
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to change progression";
      toast.error(message, { id: "change-progression" });
    }
  };

  const allSetsCompleted = exerciseEntries.every((entry) =>
    entry.sets.every((set) => set.completed)
  );

  const completedSetsCount = exerciseEntries.reduce(
    (acc, entry) => acc + entry.sets.filter((s) => s.completed).length,
    0
  );

  const totalSetsCount = exerciseEntries.reduce(
    (acc, entry) => acc + entry.sets.length,
    0
  );

  // Count exercises that have all sets completed (for exercise-level progress)
  const completedExercisesCount = exerciseEntries.filter((entry) =>
    entry.sets.length > 0 && entry.sets.every((set) => set.completed)
  ).length;

  const totalExercisesCount = exerciseEntries.filter((entry) => entry.sets.length > 0).length;

  // Progress percentage
  const progressPercentage = totalSetsCount > 0 ? (completedSetsCount / totalSetsCount) * 100 : 0;

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="max-w-4xl mx-auto px-4 py-8">
          <p>Loading workout...</p>
        </main>
      </div>
    );
  }

  if (!workout) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="max-w-4xl mx-auto px-4 py-8">
          <Card className="p-6 text-center">
            <h2 className="text-xl font-bold mb-4">No Active Workout</h2>
            <p className="text-muted-foreground mb-4">
              You need to create a workout program first.
            </p>
            <Link to="/setup">
              <Button>Create Workout Program</Button>
            </Link>
          </Card>
        </main>
      </div>
    );
  }

  if (showCompletionSummary && completionResult) {
    return (
      <CompletionSummary
        result={completionResult}
        workout={workout}
        dayNumber={dayNumber}
        dayName={dayName}
        exerciseEntries={exerciseEntries}
        workoutStartTime={workoutStartTime.current}
        workoutEndTime={workoutEndTime.current}
        onContinue={() => navigate("/workout")}
      />
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar />

      {/* Sticky Progress Header */}
      <div className="sticky top-0 z-40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="max-w-4xl mx-auto px-4 py-3">
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2 text-sm font-medium">
              <span>Day {dayNumber}</span>
              <span className="text-muted-foreground">-</span>
              <span>Week {workout.currentWeek}</span>
              <span className="text-muted-foreground ml-4">
                {completedExercisesCount}/{totalExercisesCount} exercises done
              </span>
            </div>
            <span className="text-sm text-muted-foreground">
              {Math.round(progressPercentage)}%
            </span>
          </div>
          <div className="h-2 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-primary transition-all duration-300 ease-out"
              style={{ width: `${progressPercentage}%` }}
            />
          </div>
        </div>
      </div>

      <main className="max-w-4xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="mb-6">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold" data-testid="session-title">
                {dayName} - Week {workout.currentWeek}
              </h1>
              <p className="text-muted-foreground">
                {workout.name} - Block {Math.ceil(workout.currentWeek / 7)}
              </p>
            </div>
            <Link to="/workout">
              <Button variant="outline">Cancel</Button>
            </Link>
          </div>

          {/* Prefilled indicator */}
          {isPrefilled && (
            <div className="mt-3 p-3 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg">
              <div className="flex items-center gap-2 text-blue-700 dark:text-blue-300">
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span className="font-medium">Data pulled from Hevy</span>
              </div>
              <p className="text-sm text-blue-600 dark:text-blue-400 mt-1">
                Sets are pre-filled and marked as done. Click a set to edit if needed, then complete the workout.
              </p>
            </div>
          )}

          {/* Progress indicator */}
          <div className="mt-4">
            <div className="flex justify-between text-sm text-muted-foreground mb-1">
              <span>Progress</span>
              <span data-testid="sets-progress">
                {completedSetsCount} / {totalSetsCount} sets
              </span>
            </div>
            <div className="h-2 bg-muted rounded-full overflow-hidden">
              <div
                className="h-full bg-primary transition-all duration-300"
                style={{
                  width: `${(completedSetsCount / totalSetsCount) * 100}%`,
                }}
              />
            </div>
          </div>

          {/* Auto-save indicator */}
          {completedSetsCount > 0 && (
            <div className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
              <svg className="w-3.5 h-3.5 text-green-500" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
              </svg>
              <span>Progress saved automatically</span>
            </div>
          )}
        </div>

        {/* Exercises */}
        <div className="space-y-6">
          {exerciseEntries
            .filter((entry) => entry.sets.length > 0) // Hide exercises with no sets (skipped/removed)
            .map((entry, exerciseIndex) => {
              const substitution = temporarySubstitutions.find(
                (s) => s.originalExerciseId === entry.exercise.id
              );
              return (
                <ExerciseCard
                  key={entry.exercise.id}
                  entry={entry}
                  exerciseIndex={exerciseIndex}
                  onSetChange={handleSetChange}
                  onSetComplete={handleSetComplete}
                  onSubstitute={handleOpenSubstitution}
                  onToggleUnilateral={handleToggleUnilateral}
                  onEdit={setExerciseToEdit}

                  isTemporarilySubstituted={!!substitution}
                  originalName={substitution?.originalName}
                />
              );
            })}
        </div>

        {/* Complete Workout Button */}
        <div className="mt-8 flex justify-center gap-4">
          {workout && workout.completedDaysInCurrentWeek && workout.completedDaysInCurrentWeek.length > 0 && (
            <Button
              variant="outline"
              onClick={() => setShowUndoModal(true)}
              className="text-destructive border-destructive hover:bg-destructive/10"
            >
              Undo Last Workout
            </Button>
          )}
          <Button
            size="lg"
            onClick={handleCompleteWorkout}
            disabled={!allSetsCompleted || isSubmitting}
            data-testid="complete-workout-button"
            className="min-w-[200px]"
          >
            {isSubmitting ? "Completing..." : "Complete Workout"}
          </Button>
        </div>

        {!allSetsCompleted && (
          <p className="text-center text-sm text-muted-foreground mt-2">
            Complete all sets to finish the workout
          </p>
        )}
      </main>

      {/* Exercise Substitution Modal */}
      {exerciseToSubstitute && (
        <ExerciseSubstitutionModal
          exercise={exerciseToSubstitute}
          isOpen={substitutionModalOpen}
          onClose={() => {
            setSubstitutionModalOpen(false);
            setExerciseToSubstitute(null);
          }}
          onTemporarySubstitute={handleTemporarySubstitute}
          onPermanentSubstitute={handlePermanentSubstitute}
        />
      )}

      {/* Edit Exercise Config Modal */}
      {exerciseToEdit && (
        <EditExerciseConfigModal
          exercise={exerciseToEdit}
          isOpen={exerciseToEdit !== null}
          onClose={() => setExerciseToEdit(null)}
          onSave={handleSaveExerciseConfig}
          onChangeProgression={handleChangeProgression}
        />
      )}

      {/* Pulled Substitutions Modal */}
      {showSubstitutionModal && pendingSubstitutions.length > 0 && (
        <PulledSubstitutionsModal
          substitutions={pendingSubstitutions}
          onApply={handleApplySubstitution}
          onRemove={handleRemoveFromSubstitution}
          onComplete={handleSubstitutionsComplete}
        />
      )}

      {/* Weight Discrepancy Modal */}
      {showWeightDiscrepancyModal && weightDiscrepancies.length > 0 && workout && (
        <WeightDiscrepancyModal
          discrepancies={weightDiscrepancies}
          exerciseUnit={workout.exercises[0]?.progression.type === 'Linear'
            ? (workout.exercises[0].progression as LinearProgressionDto).trainingMax.unit === 1 ? 'Kilograms' : 'Pounds'
            : 'Kilograms'}
          currentWeek={workout.currentWeek}
          exercises={workout.exercises}
          onApply={handleApplyWeightDiscrepancy}
          onComplete={handleWeightDiscrepanciesComplete}
        />
      )}

      {/* Missing Exercises Modal */}
      {showMissingExercisesModal && missingExercises.length > 0 && workout && (
        <MissingExercisesModal
          missingExercises={missingExercises}
          exerciseUnit={workout.exercises[0]?.progression.type === 'Linear'
            ? (workout.exercises[0].progression as LinearProgressionDto).trainingMax.unit === 1 ? 'Kilograms' : 'Pounds'
            : 'Kilograms'}
          onApply={handleMissingExercise}
          onComplete={handleMissingExercisesComplete}
        />
      )}

      {/* Undo Confirmation Modal */}
      {workout && (
        <UndoConfirmationModal
          isOpen={showUndoModal}
          onClose={() => setShowUndoModal(false)}
          onConfirm={handleUndoCompletion}
          dayNumber={workout.currentDay}
          weekNumber={workout.currentWeek}
          wouldRollbackWeek={false}
        />
      )}

      {/* Session Recovery Modal */}
      {showRecoveryModal && savedProgressData && (
        <SessionRecoveryModal
          savedAt={savedProgressData.savedAt}
          completedSets={savedProgressData.exercises.reduce(
            (acc, ex) => acc + ex.sets.filter((s) => s.completed).length,
            0
          )}
          onResume={handleResumeProgress}
          onStartFresh={handleStartFresh}
        />
      )}
    </div>
  );
}

// Modal for handling weight discrepancies detected when pulling from Hevy
interface WeightDiscrepancyModalProps {
  discrepancies: WeightDiscrepancy[];
  exerciseUnit: 'Kilograms' | 'Pounds';
  currentWeek: number;
  exercises: ExerciseDto[];
  onApply: (discrepancy: WeightDiscrepancy, confirmedWeight: number, decision: 'skip' | 'update') => Promise<void>;
  onComplete: () => void;
}

function WeightDiscrepancyModal({ discrepancies, exerciseUnit, currentWeek, exercises, onApply, onComplete }: WeightDiscrepancyModalProps) {
  const [decisions, setDecisions] = useState<Record<string, { weight: number; decision: 'skip' | 'update' | null }>>({});
  const [applying, setApplying] = useState(false);

  const convertWeight = (kgWeight: number) => {
    if (exerciseUnit === 'Pounds') {
      return (kgWeight / 0.453592).toFixed(1);
    }
    return kgWeight.toFixed(1);
  };

  const allDecided = discrepancies.every(disc =>
    decisions[disc.exerciseId]?.decision !== undefined && decisions[disc.exerciseId]?.decision !== null
  );

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const disc of discrepancies) {
        const decision = decisions[disc.exerciseId];
        if (decision && decision.decision) {
          await onApply(disc, decision.weight, decision.decision);
        }
      }
      onComplete();
    } catch (error) {
      console.error('Failed to apply weight discrepancies:', error);
    } finally {
      setApplying(false);
    }
  };

  useEffect(() => {
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden">
      <div className="absolute inset-0 bg-black/80" />

      <div className="relative bg-white dark:bg-zinc-900 border border-border rounded-lg shadow-2xl max-w-2xl w-full mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        <div className="p-4 border-b bg-yellow-100 dark:bg-yellow-900/50">
          <div className="flex items-center gap-2 text-yellow-800 dark:text-yellow-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <h2 className="font-bold text-lg">Weight Changes Detected</h2>
          </div>
          <p className="text-sm text-yellow-700 dark:text-yellow-300 mt-1">
            Your Hevy weights differ from prescribed. Confirm the weight and choose how to handle:
          </p>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {discrepancies.map((disc) => {
            const isLinear = disc.progressionType === 'Linear';
            const currentDecision = decisions[disc.exerciseId];

            return (
              <div key={disc.exerciseId} className="border border-border rounded-lg p-4 bg-zinc-50 dark:bg-zinc-800">
                <div className="font-medium text-base mb-3">{disc.exerciseName}</div>

                {disc.hasVaryingWeights ? (
                  <div className="mb-3">
                    <div className="text-sm text-orange-600 dark:text-orange-400 mb-2">
                      ⚠️ Sets used varying weights
                    </div>
                    <div className="text-xs text-zinc-600 dark:text-zinc-400 mb-2">
                      Weights: {disc.actualWeights.map(w => `${convertWeight(w)}${exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}`).join(', ')}
                    </div>
                    <input
                      type="number"
                      step="0.5"
                      placeholder="Confirm working weight"
                      className="w-full px-3 py-2 border border-zinc-300 dark:border-zinc-600 rounded bg-white dark:bg-zinc-700 text-sm"
                      onChange={(e) => {
                        const weight = parseFloat(e.target.value);
                        if (!isNaN(weight)) {
                          const weightInKg = exerciseUnit === 'Pounds' ? weight * 0.453592 : weight;
                          setDecisions(prev => ({
                            ...prev,
                            [disc.exerciseId]: { ...prev[disc.exerciseId], weight: weightInKg }
                          }));
                        }
                      }}
                    />
                  </div>
                ) : (
                  <div className="mb-3 space-y-1">
                    <div className="flex items-center justify-between text-sm">
                      <span className="text-zinc-500 dark:text-zinc-400">Prescribed:</span>
                      <span className="font-medium">{convertWeight(disc.prescribedWeight)} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}</span>
                    </div>
                    <div className="flex items-center justify-between text-sm">
                      <span className="text-zinc-500 dark:text-zinc-400">Actual from Hevy:</span>
                      <span className="font-medium text-blue-600 dark:text-blue-400">{convertWeight(disc.actualWeights[0])} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}</span>
                    </div>
                    <div className="flex items-center justify-between text-sm">
                      <span className="text-zinc-500 dark:text-zinc-400">Difference:</span>
                      <span className={`font-medium ${disc.actualWeights[0] - disc.prescribedWeight > 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                        {disc.actualWeights[0] - disc.prescribedWeight > 0 ? '+' : ''}{convertWeight(disc.actualWeights[0] - disc.prescribedWeight)} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}
                      </span>
                    </div>
                  </div>
                )}

                <div className="flex gap-2">
                  <button
                    onClick={() => setDecisions(prev => ({
                      ...prev,
                      [disc.exerciseId]: {
                        weight: disc.hasVaryingWeights ? (prev[disc.exerciseId]?.weight || disc.actualWeights[0]) : disc.actualWeights[0],
                        decision: 'skip'
                      }
                    }))}
                    disabled={disc.hasVaryingWeights && !currentDecision?.weight}
                    className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                      currentDecision?.decision === 'skip'
                        ? 'bg-blue-600 text-white border-blue-600'
                        : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200 disabled:opacity-50 disabled:cursor-not-allowed'
                    }`}
                  >
                    {isLinear ? 'Skip Progression This Week' : 'Temporary (This Week Only)'}
                  </button>

                  {isLinear ? (() => {
                    const weekParams = getWeekParameters(currentWeek);
                    const actualWeight = disc.hasVaryingWeights ? (currentDecision?.weight || disc.actualWeights[0]) : disc.actualWeights[0];
                    const newTm = roundToGymIncrement(actualWeight / weekParams.intensity, 'kg');
                    const exercise = exercises.find(e => e.id === disc.exerciseId);
                    const currentTm = exercise?.progression.type === 'Linear'
                      ? (exercise.progression as LinearProgressionDto).trainingMax.value
                      : null;
                    return (
                      <button
                        onClick={() => setDecisions(prev => ({
                          ...prev,
                          [disc.exerciseId]: {
                            weight: actualWeight,
                            decision: 'update'
                          }
                        }))}
                        disabled={disc.hasVaryingWeights && !currentDecision?.weight}
                        className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                          currentDecision?.decision === 'update'
                            ? 'bg-green-600 text-white border-green-600'
                            : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200 disabled:opacity-50 disabled:cursor-not-allowed'
                        }`}
                      >
                        Update TM{currentTm !== null ? ` (${currentTm} → ${newTm}kg)` : ''}
                      </button>
                    );
                  })() : (
                    <button
                      onClick={() => setDecisions(prev => ({
                        ...prev,
                        [disc.exerciseId]: {
                          weight: disc.hasVaryingWeights ? (prev[disc.exerciseId]?.weight || disc.actualWeights[0]) : disc.actualWeights[0],
                          decision: 'update'
                        }
                      }))}
                      disabled={disc.hasVaryingWeights && !currentDecision?.weight}
                      className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                        currentDecision?.decision === 'update'
                          ? 'bg-green-600 text-white border-green-600'
                          : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200 disabled:opacity-50 disabled:cursor-not-allowed'
                      }`}
                    >
                      Update Working Weight
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        <div className="p-4 border-t border-border bg-zinc-50 dark:bg-zinc-800 flex justify-end gap-2">
          <Button onClick={handleApplyAll} disabled={!allDecided || applying}>
            {applying ? 'Applying...' : 'Apply & Continue'}
          </Button>
        </div>
      </div>
    </div>
  );
}

// Modal for handling missing exercises detected when pulling from Hevy
interface MissingExercisesModalProps {
  missingExercises: MissingExercise[];
  exerciseUnit: 'Kilograms' | 'Pounds';
  onApply: (exercise: MissingExercise, decision: 'delete' | 'skip') => Promise<void>;
  onComplete: () => void;
}

function MissingExercisesModal({ missingExercises, exerciseUnit, onApply, onComplete }: MissingExercisesModalProps) {
  const [decisions, setDecisions] = useState<Record<string, 'delete' | 'skip' | null>>({});
  const [applying, setApplying] = useState(false);

  const convertWeight = (kgWeight: number) => {
    if (exerciseUnit === 'Pounds') {
      return (kgWeight / 0.453592).toFixed(1);
    }
    return kgWeight.toFixed(1);
  };

  const allDecided = missingExercises.every(ex => decisions[ex.exerciseId] !== undefined && decisions[ex.exerciseId] !== null);

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const exercise of missingExercises) {
        const decision = decisions[exercise.exerciseId];
        if (decision) {
          await onApply(exercise, decision);
        }
      }
      onComplete();
    } catch (error) {
      console.error('Failed to apply missing exercise decisions:', error);
    } finally {
      setApplying(false);
    }
  };

  useEffect(() => {
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden">
      <div className="absolute inset-0 bg-black/80" />

      <div className="relative bg-white dark:bg-zinc-900 border border-border rounded-lg shadow-2xl max-w-lg w-full mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        <div className="p-4 border-b bg-blue-100 dark:bg-blue-900/50">
          <div className="flex items-center gap-2 text-blue-800 dark:text-blue-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <h2 className="font-bold text-lg">Exercises Not Found in Hevy</h2>
          </div>
          <p className="text-sm text-blue-700 dark:text-blue-300 mt-1">
            These exercises were not completed in Hevy. How would you like to proceed?
          </p>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {missingExercises.map((exercise) => (
            <div key={exercise.exerciseId} className="border border-border rounded-lg p-3 bg-zinc-50 dark:bg-zinc-800">
              <div className="mb-3">
                <div className="font-medium text-base mb-2">{exercise.exerciseName}</div>
                <div className="text-sm text-zinc-600 dark:text-zinc-400">
                  Prescribed: {exercise.prescribedSets} sets of {exercise.prescribedReps} @ {convertWeight(exercise.prescribedWeight)} {exerciseUnit === 'Kilograms' ? 'kg' : 'lbs'}
                </div>
                <div className="text-xs text-zinc-500 dark:text-zinc-500 mt-1">
                  This exercise was not found in your Hevy workout
                </div>
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'skip' }))}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[exercise.exerciseId] === 'skip'
                      ? 'bg-blue-600 text-white border-blue-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Skip This Week
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [exercise.exerciseId]: 'delete' }))}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[exercise.exerciseId] === 'delete'
                      ? 'bg-red-600 text-white border-red-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Remove from Program
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-border bg-zinc-50 dark:bg-zinc-800 flex justify-end gap-2">
          <Button onClick={handleApplyAll} disabled={!allDecided || applying}>
            {applying ? 'Applying...' : 'Apply & Continue'}
          </Button>
        </div>
      </div>
    </div>
  );
}

// Modal for handling substitutions detected when pulling from Hevy
interface PulledSubstitutionsModalProps {
  substitutions: DetectedSubstitution[];
  onApply: (sub: DetectedSubstitution, isPermanent: boolean) => Promise<void>;
  onRemove: (sub: DetectedSubstitution) => Promise<void>;
  onComplete: () => void;
}

function PulledSubstitutionsModal({ substitutions, onApply, onRemove, onComplete }: PulledSubstitutionsModalProps) {
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

  // Prevent body scroll when modal is open
  useEffect(() => {
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden">
      {/* Backdrop - fully opaque */}
      <div className="absolute inset-0 bg-black/80" />

      {/* Modal content */}
      <div className="relative bg-white dark:bg-zinc-900 border border-border rounded-lg shadow-2xl max-w-lg w-full mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        {/* Header */}
        <div className="p-4 border-b bg-yellow-100 dark:bg-yellow-900/50">
          <div className="flex items-center gap-2 text-yellow-800 dark:text-yellow-200">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <h2 className="font-bold text-lg">Exercise Substitutions Detected</h2>
          </div>
          <p className="text-sm text-yellow-700 dark:text-yellow-300 mt-1">
            You used different exercises in Hevy. Choose how to handle each:
          </p>
        </div>

        {/* Substitutions List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-white dark:bg-zinc-900">
          {substitutions.map((sub) => (
            <div key={sub.originalExerciseId} className="border border-border rounded-lg p-3 bg-zinc-50 dark:bg-zinc-800">
              <div className="mb-3">
                <div className="flex items-center gap-2 text-sm">
                  <span className="text-zinc-500 dark:text-zinc-400">Program:</span>
                  <span className="font-medium line-through text-red-600 dark:text-red-400">{sub.originalExerciseName}</span>
                </div>
                <div className="flex items-center gap-2 text-sm mt-1">
                  <span className="text-zinc-500 dark:text-zinc-400">Hevy:</span>
                  <span className="font-medium text-green-600 dark:text-green-400">{sub.hevyExerciseName}</span>
                </div>
                <div className="text-xs text-zinc-500 dark:text-zinc-400 mt-1">
                  {sub.sets.length} sets: {sub.sets.map(s => `${s.weight}kg × ${s.reps}`).join(', ')}
                </div>
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'temporary' }))}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'temporary'
                      ? 'bg-blue-600 text-white border-blue-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  This Session Only
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'permanent' }))}
                  className={`flex-1 px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'permanent'
                      ? 'bg-green-600 text-white border-green-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-zinc-100 dark:hover:bg-zinc-600 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Permanent Change
                </button>
                <button
                  onClick={() => setDecisions(prev => ({ ...prev, [sub.originalExerciseId]: 'remove' }))}
                  className={`px-3 py-2 text-sm rounded border transition-colors font-medium ${
                    decisions[sub.originalExerciseId] === 'remove'
                      ? 'bg-red-600 text-white border-red-600'
                      : 'bg-white dark:bg-zinc-700 hover:bg-red-50 dark:hover:bg-red-900/30 border-zinc-300 dark:border-zinc-600 text-zinc-700 dark:text-zinc-200'
                  }`}
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-border bg-zinc-50 dark:bg-zinc-800 flex justify-end gap-2">
          <Button
            onClick={handleApplyAll}
            disabled={!allDecided || applying}
          >
            {applying ? 'Applying...' : 'Apply & Continue'}
          </Button>
        </div>
      </div>
    </div>
  );
}

interface ExerciseCardProps {
  entry: ExerciseEntry;
  exerciseIndex: number;
  onSetChange: (
    exerciseIndex: number,
    setIndex: number,
    field: "weight" | "reps",
    value: number
  ) => void;
  onSetComplete: (exerciseIndex: number, setIndex: number) => void;
  onSubstitute: (exercise: ExerciseDto) => void;
  onToggleUnilateral: (exercise: ExerciseDto) => void;
  onEdit: (exercise: ExerciseDto) => void;
  isTemporarilySubstituted: boolean;
  originalName?: string;
}

function ExerciseCard({
  entry,
  exerciseIndex,
  onSetChange,
  onSetComplete,
  onSubstitute,
  onToggleUnilateral,
  onEdit,
  isTemporarilySubstituted,
  originalName,
}: ExerciseCardProps) {
  const allCompleted = entry.sets.every((s) => s.completed);
  const isRepsPerSet = entry.exercise.progression.type === "RepsPerSet";
  const repsPerSetProg = isRepsPerSet ? (entry.exercise.progression as RepsPerSetProgressionDto) : null;

  return (
    <Card
      className={`p-4 ${allCompleted ? "border-green-500 bg-green-50 dark:bg-green-950/20" : ""}`}
      data-testid={`exercise-card-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}
    >
      <div className="flex items-center justify-between mb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-lg">{entry.exercise.name}</h3>
            {isTemporarilySubstituted && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400">
                Temp Sub
              </span>
            )}
            {repsPerSetProg?.isUnilateral && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400">
                Per Side
              </span>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {entry.exercise.progression.type} Progression
            {entry.isAmrapExercise && " - AMRAP on last set"}
            {isTemporarilySubstituted && originalName && (
              <span className="ml-2 text-yellow-600 dark:text-yellow-400">
                (replacing {originalName})
              </span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Edit button */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onEdit(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            title="Edit exercise configuration"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </Button>
          {/* Unilateral toggle button - only for RepsPerSet */}
          {isRepsPerSet && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onToggleUnilateral(entry.exercise)}
              className={`text-xs px-2 ${repsPerSetProg?.isUnilateral ? "text-blue-600 dark:text-blue-400" : "text-muted-foreground hover:text-foreground"}`}
              title={repsPerSetProg?.isUnilateral ? "Switch to bilateral (both sides together)" : "Switch to unilateral (one side at a time)"}
            >
              {repsPerSetProg?.isUnilateral ? "1-Arm" : "2-Arm"}
            </Button>
          )}
          {/* Substitute button */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onSubstitute(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            title="Substitute exercise"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
          </Button>
          {allCompleted && (
            <div className="text-green-500">
              <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
          )}
        </div>
      </div>

      {/* Sets */}
      <div className="space-y-3">
        <div className="grid grid-cols-12 gap-2 text-xs font-medium text-muted-foreground">
          <div className="col-span-1">Set</div>
          <div className="col-span-4">Weight ({entry.weightUnit})</div>
          <div className="col-span-4">Reps</div>
          <div className="col-span-3">Done</div>
        </div>

        {entry.sets.map((set, setIndex) => {
          // Calculate AMRAP progression hint for linear exercises
          const getAmrapProgressionHint = () => {
            if (!set.isAmrap || entry.exercise.progression.type !== "Linear") return null;
            const progressionThreshold = entry.targetReps + 3;
            return progressionThreshold;
          };
          const amrapHint = getAmrapProgressionHint();

          return (
            <div key={set.setNumber}>
              {/* AMRAP Set Visual Emphasis */}
              {set.isAmrap && !set.completed && (
                <div className="mb-2 p-3 rounded-lg bg-gradient-to-r from-orange-100 to-amber-100 dark:from-orange-950/40 dark:to-amber-950/40 border border-orange-200 dark:border-orange-800">
                  <div className="flex items-center gap-2 text-orange-700 dark:text-orange-300 font-semibold">
                    <span className="text-lg">🔥</span>
                    <span>FINAL SET - AMRAP</span>
                  </div>
                  <p className="text-sm text-orange-600 dark:text-orange-400 mt-1">
                    As Many Reps As Possible!
                    {amrapHint && (
                      <span className="ml-2 font-medium">
                        Try for {amrapHint}+ for TM ↑
                      </span>
                    )}
                  </p>
                </div>
              )}
              <div
                className={`grid grid-cols-12 gap-2 items-center ${
                  set.completed ? "opacity-60" : ""
                } ${set.isAmrap && !set.completed ? "p-2 rounded-lg bg-orange-50/50 dark:bg-orange-950/20 border border-orange-100 dark:border-orange-900/50" : ""}`}
                data-testid={`set-row-${set.setNumber}`}
              >
                <div className="col-span-1 font-medium">
                  {set.setNumber}
                  {set.isAmrap && (
                    <span className="text-xs text-orange-500 dark:text-orange-400 ml-1">🔥</span>
                  )}
                </div>
                <div className="col-span-4">
                  <Input
                    type="number"
                    value={set.weight}
                    onChange={(e) =>
                      onSetChange(
                        exerciseIndex,
                        setIndex,
                        "weight",
                        parseFloat(e.target.value) || 0
                      )
                    }
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 dark:border-orange-700 focus:border-orange-500 focus:ring-orange-500" : ""}`}
                    data-testid={`weight-input-${set.setNumber}`}
                    disabled={set.completed}
                  />
                </div>
                <div className="col-span-4">
                  <Input
                    type="number"
                    value={set.reps}
                    onChange={(e) =>
                      onSetChange(
                        exerciseIndex,
                        setIndex,
                        "reps",
                        parseInt(e.target.value) || 0
                      )
                    }
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 dark:border-orange-700 focus:border-orange-500 focus:ring-orange-500" : ""}`}
                    data-testid={`reps-input-${set.setNumber}`}
                    disabled={set.completed}
                  />
                </div>
                <div className="col-span-3">
                  <Button
                    variant={set.completed ? "default" : set.isAmrap ? "default" : "outline"}
                    size="sm"
                    className={`w-full h-8 ${set.isAmrap && !set.completed ? "bg-orange-500 hover:bg-orange-600 text-white" : ""}`}
                    onClick={() => onSetComplete(exerciseIndex, setIndex)}
                    data-testid={`complete-set-${set.setNumber}`}
                  >
                    {set.completed ? "Done" : set.isAmrap ? "Log AMRAP" : "Log"}
                  </Button>
                </div>
              </div>
            </div>
          );
        })}
      </div>

    </Card>
  );
}

interface CompletionSummaryProps {
  result: CompleteDayResult;
  workout: WorkoutDto;
  dayNumber: DayNumber;
  dayName: string;
  exerciseEntries: ExerciseEntry[];
  workoutStartTime: Date;
  workoutEndTime: Date;
  onContinue: () => void;
}

function CompletionSummary({
  result,
  workout,
  dayNumber,
  dayName,
  exerciseEntries,
  workoutStartTime,
  workoutEndTime,
  onContinue,
}: CompletionSummaryProps) {
  const { isConfigured, isValid } = useHevy();
  const [isSyncingToHevy, setIsSyncingToHevy] = useState(false);
  const [hevySynced, setHevySynced] = useState(false);
  const [routineLifecycleCompleted, setRoutineLifecycleCompleted] = useState(false);
  const [routineLifecycleMessage, setRoutineLifecycleMessage] = useState<string | null>(null);

  // Handle routine lifecycle when week progresses
  const handleRoutineLifecycleOnWeekProgress = async () => {
    if (!result.weekProgressed || result.programComplete) return;
    if (!isConfigured || !isValid) return;
    if (routineLifecycleCompleted) return;

    try {
      const lifecycleResult = await handleRoutineLifecycle(
        workout,
        dayNumber,
        result.weekNumber,
        result.newCurrentWeek
      );

      if (lifecycleResult.success) {
        setRoutineLifecycleMessage(lifecycleResult.message);
        toast.success(lifecycleResult.message);
      } else {
        setRoutineLifecycleMessage(`Routine update: ${lifecycleResult.message}`);
        // Don't show error toast, just log - not critical
        console.warn('Routine lifecycle warning:', lifecycleResult.message);
      }
      setRoutineLifecycleCompleted(true);
    } catch (error) {
      console.error('Failed to handle routine lifecycle:', error);
      setRoutineLifecycleCompleted(true);
    }
  };

  // Auto-trigger routine lifecycle when component mounts and week progressed
  useEffect(() => {
    if (result.weekProgressed && isConfigured && isValid && !routineLifecycleCompleted) {
      handleRoutineLifecycleOnWeekProgress();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [result.weekProgressed, isConfigured, isValid]);

  const handleSendToHevy = async () => {
    if (!workout) return;

    setIsSyncingToHevy(true);
    try {
      // Convert exercise entries to the format expected by the sync service
      const completedExercises: CompletedExerciseData[] = exerciseEntries.map((entry) => ({
        exercise: entry.exercise,
        sets: entry.sets
          .filter((set) => set.completed)
          .map((set) => ({
            setNumber: set.setNumber,
            weight: set.weight,
            reps: set.reps,
            isAmrap: set.isAmrap,
          })),
        weightUnit: entry.weightUnit,
      }));

      const syncResult = await createCompletedWorkoutInHevy(
        workout,
        dayNumber,
        completedExercises,
        workoutStartTime,
        workoutEndTime
      );

      if (syncResult.success) {
        toast.success(syncResult.message);
        setHevySynced(true);
      } else {
        toast.error(syncResult.message);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to sync to Hevy';
      toast.error(message);
    } finally {
      setIsSyncingToHevy(false);
    }
  };

  const getOutcomeStyle = (change: string) => {
    if (change.toLowerCase().includes("increased") || change.toLowerCase().includes("added")) {
      return "text-green-600 bg-green-100 dark:bg-green-900/30";
    }
    if (change.toLowerCase().includes("decreased") || change.toLowerCase().includes("reduced")) {
      return "text-red-600 bg-red-100 dark:bg-red-900/30";
    }
    if (change.toLowerCase().includes("deload")) {
      return "text-blue-600 bg-blue-100 dark:bg-blue-900/30";
    }
    return "text-yellow-600 bg-yellow-100 dark:bg-yellow-900/30";
  };

  const getOutcomeLabel = (change: string): string => {
    if (change.toLowerCase().includes("increased") || change.toLowerCase().includes("added")) {
      return "SUCCESS";
    }
    if (change.toLowerCase().includes("decreased") || change.toLowerCase().includes("reduced")) {
      return "FAILED";
    }
    if (change.toLowerCase().includes("deload")) {
      return "DELOAD";
    }
    return "MAINTAINED";
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="max-w-4xl mx-auto px-4 py-8">
        {/* Completion Header */}
        <Card className="p-6 mb-6 text-center border-green-500 bg-green-50 dark:bg-green-950/20">
          <div className="flex justify-center mb-4">
            <div className="w-16 h-16 bg-green-500 rounded-full flex items-center justify-center">
              <svg
                className="w-10 h-10 text-white"
                fill="currentColor"
                viewBox="0 0 20 20"
              >
                <path
                  fillRule="evenodd"
                  d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
          </div>
          <h1 className="text-2xl font-bold text-green-700 dark:text-green-400" data-testid="completion-title">
            {result.programComplete ? "Program Complete!" : "Workout Complete!"}
          </h1>
          <p className="text-muted-foreground mt-2">
            {dayName} - Week {result.weekNumber}
            {result.isDeloadWeek && " (Deload Week)"}
          </p>
          <p className="text-sm text-muted-foreground">
            {result.exercisesCompleted} exercises completed
          </p>

          {/* Week progression notification */}
          {result.weekProgressed && !result.programComplete && (
            <div className="mt-4 p-3 bg-primary/10 rounded-lg" data-testid="week-progressed-notice">
              <p className="font-semibold text-primary">
                Week Complete! Moving to Week {result.newCurrentWeek}
                {result.isDeloadWeek && " (Deload Week)"}
              </p>
              {isConfigured && isValid && routineLifecycleMessage && (
                <p className="text-sm text-muted-foreground mt-1">
                  {routineLifecycleMessage}
                </p>
              )}
            </div>
          )}

          {result.programComplete && (
            <div className="mt-4 p-3 bg-gradient-to-r from-green-500 to-blue-500 text-white rounded-lg" data-testid="program-complete-notice">
              <p className="font-bold text-lg">
                Congratulations! You've completed the {workout.totalWeeks}-week program!
              </p>
            </div>
          )}
        </Card>

        {/* Progression Changes */}
        <Card className="p-6 mb-6">
          <h2 className="text-xl font-bold mb-4" data-testid="progression-changes-title">
            Progression Results
          </h2>
          <div className="space-y-3">
            {result.progressionChanges.map((change, index) => (
              <div
                key={index}
                className={`p-3 rounded-lg ${getOutcomeStyle(change.change)}`}
                data-testid={`progression-change-${index}`}
              >
                <div className="flex items-center justify-between">
                  <div>
                    <span className="font-semibold">{change.exerciseName}</span>
                    <span className="text-sm ml-2">({change.progressionType})</span>
                  </div>
                  <span
                    className="text-xs font-bold px-2 py-1 rounded"
                    data-testid={`outcome-label-${index}`}
                  >
                    {getOutcomeLabel(change.change)}
                  </span>
                </div>
                <p className="text-sm mt-1" data-testid={`change-description-${index}`}>
                  {change.change}
                </p>
              </div>
            ))}
          </div>
        </Card>

        {/* Next Session Preview */}
        <Card className="p-6 mb-6">
          <h2 className="text-xl font-bold mb-4" data-testid="next-session-title">
            {result.programComplete
              ? "Final Session Summary"
              : `Next ${dayName} Session (Week ${result.weekProgressed ? result.newCurrentWeek : result.weekNumber + 1})`}
          </h2>
          <div className="space-y-4">
            {exerciseEntries.map((entry, index) => {
              const change = result.progressionChanges.find(
                (c) => c.exerciseId === entry.exercise.id
              );
              return (
                <div
                  key={entry.exercise.id}
                  className="border-l-4 border-primary pl-4 py-2"
                  data-testid={`next-session-exercise-${index}`}
                >
                  <div className="font-semibold">{entry.exercise.name}</div>
                  <div className="text-sm text-muted-foreground">
                    <span data-testid={`next-sets-${index}`}>
                      {change?.newValue || `${entry.targetSets} sets`}
                    </span>
                    {" x "}
                    <span data-testid={`next-reps-${index}`}>
                      {entry.targetReps} reps
                    </span>
                    {" @ "}
                    <span data-testid={`next-weight-${index}`}>
                      {entry.targetWeight.toFixed(1)} {entry.weightUnit}
                    </span>
                  </div>
                  {change && (
                    <div
                      className={`text-xs mt-1 ${
                        getOutcomeLabel(change.change) === "SUCCESS"
                          ? "text-green-600"
                          : getOutcomeLabel(change.change) === "FAILED"
                          ? "text-red-600"
                          : "text-yellow-600"
                      }`}
                    >
                      {change.change}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </Card>

        {/* Hevy Sync Option */}
        {isConfigured && isValid && (
          <Card className="p-6 mb-6">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-semibold flex items-center gap-2">
                  <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  Sync to Hevy
                </h3>
                <p className="text-sm text-muted-foreground mt-1">
                  Send this workout to your Hevy app
                </p>
              </div>
              <Button
                onClick={handleSendToHevy}
                disabled={isSyncingToHevy || hevySynced}
                variant={hevySynced ? "outline" : "default"}
              >
                {isSyncingToHevy ? (
                  <>
                    <svg className="h-4 w-4 mr-2 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    Syncing...
                  </>
                ) : hevySynced ? (
                  <>
                    <svg className="h-4 w-4 mr-2" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                    </svg>
                    Synced to Hevy
                  </>
                ) : (
                  'Send to Hevy'
                )}
              </Button>
            </div>
          </Card>
        )}

        {/* Next Week Preview Section */}
        {!result.programComplete && result.weekProgressed && result.newCurrentWeek <= workout.totalWeeks && (
          <Card className="p-6 mb-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-xl font-bold flex items-center gap-2">
                <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7l5 5m0 0l-5 5m5-5H6" />
                </svg>
                Next Week: Week {result.newCurrentWeek}
              </h2>
              {result.isDeloadWeek && (
                <span className="text-sm px-2 py-1 bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400 rounded">
                  Deload Week
                </span>
              )}
            </div>
            <p className="text-sm text-muted-foreground mb-4">
              You've completed the week! Here's what's coming up next.
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {Array.from({ length: workout.daysPerWeek }, (_, i) => i + 1).map((day) => {
                const dayExercises = workout.exercises.filter(e => e.assignedDay === day);
                return (
                  <div key={day} className="p-3 border rounded-lg bg-muted/20">
                    <h4 className="font-semibold mb-2">W{result.newCurrentWeek} D{day}</h4>
                    <div className="space-y-1">
                      {dayExercises.map(ex => (
                        <div key={ex.id} className="text-sm text-muted-foreground">
                          {ex.name}
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })}
            </div>
          </Card>
        )}

        {/* Action Buttons */}
        <div className="flex justify-center gap-4">
          <Button
            variant="outline"
            onClick={onContinue}
            data-testid="back-to-workout-button"
          >
            Back to Workout
          </Button>
          <Button
            onClick={onContinue}
            data-testid="continue-button"
          >
            Continue
          </Button>
        </div>
      </main>
    </div>
  );
}

// Modal for session recovery
interface SessionRecoveryModalProps {
  savedAt: string;
  completedSets: number;
  onResume: () => void;
  onStartFresh: () => void;
}

function SessionRecoveryModal({ savedAt, completedSets, onResume, onStartFresh }: SessionRecoveryModalProps) {
  const savedDate = new Date(savedAt);
  const timeAgo = getTimeAgo(savedDate);

  useEffect(() => {
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-hidden">
      <div className="absolute inset-0 bg-black/80" />

      <div className="relative bg-white dark:bg-zinc-900 border border-border rounded-lg shadow-2xl max-w-md w-full mx-4">
        <div className="p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-12 h-12 bg-blue-100 dark:bg-blue-900/30 rounded-full flex items-center justify-center">
              <svg className="w-6 h-6 text-blue-600 dark:text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <div>
              <h2 className="text-lg font-bold">Incomplete Workout Found</h2>
              <p className="text-sm text-muted-foreground">Saved {timeAgo}</p>
            </div>
          </div>

          <div className="p-4 bg-muted/50 rounded-lg mb-6">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Completed sets</span>
              <span className="font-semibold">{completedSets}</span>
            </div>
          </div>

          <p className="text-sm text-muted-foreground mb-6">
            You have an incomplete workout. Would you like to resume where you left off or start fresh?
          </p>

          <div className="flex gap-3">
            <Button
              variant="outline"
              className="flex-1"
              onClick={onStartFresh}
            >
              Start Fresh
            </Button>
            <Button
              className="flex-1"
              onClick={onResume}
            >
              Resume
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

// Helper function to get human-readable time ago
function getTimeAgo(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins} minute${diffMins === 1 ? '' : 's'} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours === 1 ? '' : 's'} ago`;
  if (diffDays === 1) return 'yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  return date.toLocaleDateString();
}
