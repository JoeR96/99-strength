import { useState, useMemo } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useCurrentWorkout } from "@/hooks/useWorkouts";
import { workoutsApi } from "@/api/workouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Navbar } from "@/components/layout/Navbar";
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  ExercisePerformanceRequest,
  CompleteDayResult,
  WeightUnit,
  DayNumber,
} from "@/types/workout";

interface SetEntry {
  setNumber: number;
  weight: number;
  reps: number;
  isAmrap: boolean;
  completed: boolean;
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
  const { data: workout, isLoading, refetch } = useCurrentWorkout();
  const [exerciseEntries, setExerciseEntries] = useState<ExerciseEntry[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [completionResult, setCompletionResult] = useState<CompleteDayResult | null>(null);
  const [showCompletionSummary, setShowCompletionSummary] = useState(false);

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
  useMemo(() => {
    if (dayExercises.length > 0 && exerciseEntries.length === 0) {
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
          targetSets = prog.baseSetsPerExercise;
          targetReps = 10; // Default working reps
          targetWeight = prog.trainingMax.value * 0.7; // ~70% of TM
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
  }, [dayExercises, exerciseEntries.length]);

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
      const performances: ExercisePerformanceRequest[] = exerciseEntries.map(
        (entry) => ({
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
        })
      );

      const result = await workoutsApi.completeDay(workout.id, dayNumber, {
        performances,
      });

      setCompletionResult(result);
      setShowCompletionSummary(true);
      await refetch();
    } catch (error) {
      console.error("Failed to complete workout:", error);
      alert("Failed to complete workout. Please try again.");
    } finally {
      setIsSubmitting(false);
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
          {exerciseEntries.map((entry, exerciseIndex) => (
            <ExerciseCard
              key={entry.exercise.id}
              entry={entry}
              exerciseIndex={exerciseIndex}
              onSetChange={handleSetChange}
              onSetComplete={handleSetComplete}
            />
          ))}
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
}

function ExerciseCard({
  entry,
  exerciseIndex,
  onSetChange,
  onSetComplete,
}: ExerciseCardProps) {
  const allCompleted = entry.sets.every((s) => s.completed);

  return (
    <Card
      className={`p-4 ${allCompleted ? "border-green-500 bg-green-50 dark:bg-green-950/20" : ""}`}
      data-testid={`exercise-card-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}
    >
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="font-semibold text-lg">{entry.exercise.name}</h3>
          <p className="text-sm text-muted-foreground">
            {entry.exercise.progression.type} Progression
            {entry.isAmrapExercise && " - AMRAP on last set"}
          </p>
        </div>
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
  workout: any;
  dayNumber: DayNumber;
  dayName: string;
  exerciseEntries: ExerciseEntry[];
  onContinue: () => void;
}

function CompletionSummary({
  result,
  workout: _workout,
  dayNumber: _dayNumber,
  dayName,
  exerciseEntries,
  onContinue,
}: CompletionSummaryProps) {
  void _workout;
  void _dayNumber;
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
