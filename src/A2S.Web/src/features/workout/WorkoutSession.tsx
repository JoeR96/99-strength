import { useState, useMemo, useRef, useEffect } from "react";
import { useParams, useNavigate, Link, useLocation } from "react-router-dom";
import { useCurrentWorkout, useSubstituteExercise } from "@/hooks/useWorkouts";
import { workoutsApi } from "@/api/workouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Navbar } from "@/components/layout/Navbar";
import { useHevy } from "@/contexts/HevyContext";
import { createCompletedWorkoutInHevy, handleRoutineLifecycle, type CompletedExerciseData } from "@/services/hevySyncService";
import { ExerciseSubstitutionModal, type RepsPerSetConfig } from "./ExerciseSubstitutionModal";
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
} from "@/types/workout";
import type { PulledWorkoutData, DetectedSubstitution } from "@/services/hevySyncService";

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

export function WorkoutSession() {
  const { day } = useParams<{ day: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { data: workout, isLoading, refetch } = useCurrentWorkout();
  const substituteExercise = useSubstituteExercise();
  const [exerciseEntries, setExerciseEntries] = useState<ExerciseEntry[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [completionResult, setCompletionResult] = useState<CompleteDayResult | null>(null);
  const [showCompletionSummary, setShowCompletionSummary] = useState(false);
  const [isPrefilled, setIsPrefilled] = useState(false);
  const [showSubstitutionModal, setShowSubstitutionModal] = useState(false);
  const [pendingSubstitutions, setPendingSubstitutions] = useState<DetectedSubstitution[]>([]);

  // Get pulled data from navigation state (if coming from "Pull Workout" button)
  const locationState = location.state as {
    pulledData?: PulledWorkoutData[];
    pulledSubstitutions?: DetectedSubstitution[];
  } | null;
  const pulledData = locationState?.pulledData;
  const pulledSubstitutions = locationState?.pulledSubstitutions;

  // Exercise substitution state
  const [substitutionModalOpen, setSubstitutionModalOpen] = useState(false);
  const [exerciseToSubstitute, setExerciseToSubstitute] = useState<ExerciseDto | null>(null);
  const [temporarySubstitutions, setTemporarySubstitutions] = useState<TemporarySubstitution[]>([]);

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

  // Handle completing all substitution decisions
  const handleSubstitutionsComplete = () => {
    setShowSubstitutionModal(false);
    setIsPrefilled(true);
    toast.success("Workout data prefilled! Review and complete workout when ready.");
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
      // TODO: If repsConfig is provided, the backend should handle the progression type change
      // For now, we just substitute the name and handle the repsConfig locally
      await substituteExercise.mutateAsync({
        workoutId: workout.id,
        request: {
          exerciseId: originalExercise.id,
          newExerciseName: substituteTemplate.name,
          reason: repsConfig
            ? `User substitution - switched to RepsPerSet (${repsConfig.sets}×${repsConfig.minReps}-${repsConfig.targetReps}-${repsConfig.maxReps})`
            : "User substitution",
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
        </div>

        {/* Exercises */}
        <div className="space-y-6">
          {exerciseEntries.map((entry, exerciseIndex) => {
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
                isTemporarilySubstituted={!!substitution}
                originalName={substitution?.originalName}
              />
            );
          })}
        </div>

        {/* Complete Workout Button */}
        <div className="mt-8 flex justify-center">
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

      {/* Pulled Substitutions Modal */}
      {showSubstitutionModal && pendingSubstitutions.length > 0 && (
        <PulledSubstitutionsModal
          substitutions={pendingSubstitutions}
          onApply={handleApplySubstitution}
          onComplete={handleSubstitutionsComplete}
        />
      )}
    </div>
  );
}

// Modal for handling substitutions detected when pulling from Hevy
interface PulledSubstitutionsModalProps {
  substitutions: DetectedSubstitution[];
  onApply: (sub: DetectedSubstitution, isPermanent: boolean) => Promise<void>;
  onComplete: () => void;
}

function PulledSubstitutionsModal({ substitutions, onApply, onComplete }: PulledSubstitutionsModalProps) {
  const [decisions, setDecisions] = useState<Record<string, 'temporary' | 'permanent' | null>>({});
  const [applying, setApplying] = useState(false);

  const allDecided = substitutions.every(sub => decisions[sub.originalExerciseId] !== undefined && decisions[sub.originalExerciseId] !== null);

  const handleApplyAll = async () => {
    setApplying(true);
    try {
      for (const sub of substitutions) {
        const decision = decisions[sub.originalExerciseId];
        if (decision) {
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
  isTemporarilySubstituted: boolean;
  originalName?: string;
}

function ExerciseCard({
  entry,
  exerciseIndex,
  onSetChange,
  onSetComplete,
  onSubstitute,
  isTemporarilySubstituted,
  originalName,
}: ExerciseCardProps) {
  const allCompleted = entry.sets.every((s) => s.completed);

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

        {entry.sets.map((set, setIndex) => (
          <div
            key={set.setNumber}
            className={`grid grid-cols-12 gap-2 items-center ${
              set.completed ? "opacity-60" : ""
            }`}
            data-testid={`set-row-${set.setNumber}`}
          >
            <div className="col-span-1 font-medium">
              {set.setNumber}
              {set.isAmrap && (
                <span className="text-xs text-primary ml-1">*</span>
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
                className="h-8"
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
                className="h-8"
                data-testid={`reps-input-${set.setNumber}`}
                disabled={set.completed}
              />
            </div>
            <div className="col-span-3">
              <Button
                variant={set.completed ? "default" : "outline"}
                size="sm"
                className="w-full h-8"
                onClick={() => onSetComplete(exerciseIndex, setIndex)}
                data-testid={`complete-set-${set.setNumber}`}
              >
                {set.completed ? "Done" : "Log"}
              </Button>
            </div>
          </div>
        ))}
      </div>

      {entry.isAmrapExercise && (
        <p className="text-xs text-muted-foreground mt-3">
          * AMRAP set - enter as many reps as you completed
        </p>
      )}
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
                Congratulations! You've completed the 21-week program!
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
              : `Next ${dayName} Session (Week ${result.newCurrentWeek})`}
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
        {!result.programComplete && result.weekProgressed && workout.currentWeek < workout.totalWeeks && (
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
