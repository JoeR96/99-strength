import { useState, useEffect, useCallback } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Navbar } from "@/components/layout/Navbar";
import { useHevy } from "@/contexts/HevyContext";
import { createCompletedWorkoutInHevy, handleRoutineLifecycle, type CompletedExerciseData } from "@/services/hevySyncService";
import toast from "react-hot-toast";
import type { CompleteDayResult, WorkoutDto, DayNumber, ExerciseEntry } from "./workoutSessionTypes";

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

export function CompletionSummary({
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

  const handleRoutineLifecycleOnWeekProgress = useCallback(async () => {
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
        console.warn('Routine lifecycle warning:', lifecycleResult.message);
      }
      setRoutineLifecycleCompleted(true);
    } catch (error) {
      console.error('Failed to handle routine lifecycle:', error);
      setRoutineLifecycleCompleted(true);
    }
  }, [result.weekProgressed, result.programComplete, result.weekNumber, result.newCurrentWeek, isConfigured, isValid, routineLifecycleCompleted, workout, dayNumber]);

  useEffect(() => {
    if (result.weekProgressed && isConfigured && isValid && !routineLifecycleCompleted) {
      handleRoutineLifecycleOnWeekProgress();
    }
  }, [result.weekProgressed, isConfigured, isValid, routineLifecycleCompleted, handleRoutineLifecycleOnWeekProgress]);

  const handleSendToHevy = async () => {
    if (!workout) return;

    setIsSyncingToHevy(true);
    try {
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
        workoutEndTime,
        result.progressionChanges
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
      return "text-green-600 bg-green-100";
    }
    if (change.toLowerCase().includes("decreased") || change.toLowerCase().includes("reduced")) {
      return "text-red-600 bg-red-100";
    }
    if (change.toLowerCase().includes("deload")) {
      return "text-blue-600 bg-blue-100";
    }
    return "text-yellow-600 bg-yellow-100";
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
        <Card className="p-6 mb-6 text-center border-green-500 bg-green-50">
          <div className="flex justify-center mb-4">
            <div className="w-16 h-16 bg-green-500 rounded-full flex items-center justify-center">
              <svg className="w-10 h-10 text-white" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
              </svg>
            </div>
          </div>
          <h1 className="text-2xl font-bold text-green-700" data-testid="completion-title">
            {result.programComplete ? "Program Complete!" : "Workout Complete!"}
          </h1>
          <p className="text-muted-foreground mt-2">
            {dayName} - Week {result.weekNumber}
            {result.isDeloadWeek && " (Deload Week)"}
          </p>
          <p className="text-sm text-muted-foreground">
            {result.exercisesCompleted} exercises completed
          </p>

          {result.weekProgressed && !result.programComplete && (
            <div className="mt-4 p-3 bg-primary/10 rounded-lg" data-testid="week-progressed-notice">
              <p className="font-semibold text-primary">
                Week Complete! Moving to Week {result.newCurrentWeek}
                {result.isDeloadWeek && " (Deload Week)"}
              </p>
              {isConfigured && isValid && routineLifecycleMessage && (
                <p className="text-sm text-muted-foreground mt-1">{routineLifecycleMessage}</p>
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
          <h2 className="text-xl font-bold mb-4" data-testid="progression-changes-title">Progression Results</h2>
          <div className="space-y-3">
            {result.progressionChanges.map((change, index) => (
              <div key={index} className={`p-3 rounded-lg ${getOutcomeStyle(change.change)}`} data-testid={`progression-change-${index}`}>
                <div className="flex items-center justify-between">
                  <div>
                    <span className="font-semibold">{change.exerciseName}</span>
                  </div>
                  <span className="text-xs font-bold px-2 py-1 rounded" data-testid={`outcome-label-${index}`}>
                    {getOutcomeLabel(change.change)}
                  </span>
                </div>
                <p className="text-sm mt-1" data-testid={`change-description-${index}`}>{change.change}</p>
              </div>
            ))}
          </div>
        </Card>

        {/* Next Session Preview (plan computed server-side after progression) */}
        {(result.programComplete || result.nextSessionExercises?.length > 0) && (
          <Card className="p-6 mb-6">
            <h2 className="text-xl font-bold mb-4" data-testid="next-session-title">
              {result.programComplete
                ? "Final Session Summary"
                : `Next ${dayName} Session (Week ${result.weekNumber + 1})`}
            </h2>
            <div className="space-y-4">
              {result.programComplete
                ? exerciseEntries.map((entry, index) => (
                    <div key={entry.exercise.id} className="border-l-4 border-primary pl-4 py-2" data-testid={`next-session-exercise-${index}`}>
                      <div className="font-semibold">{entry.exercise.name}</div>
                      <div className="text-sm text-muted-foreground">
                        <span data-testid={`next-sets-${index}`}>{entry.targetSets} sets</span>
                        {" x "}
                        <span data-testid={`next-reps-${index}`}>{entry.targetReps} reps</span>
                        {" @ "}
                        <span data-testid={`next-weight-${index}`}>{entry.targetWeight.toFixed(1)} {entry.weightUnit}</span>
                      </div>
                    </div>
                  ))
                : result.nextSessionExercises.map((next, index) => {
                    const change = result.progressionChanges.find((c) => c.exerciseId === next.exerciseId);
                    return (
                      <div key={next.exerciseId} className="border-l-4 border-primary pl-4 py-2" data-testid={`next-session-exercise-${index}`}>
                        <div className="font-semibold">{next.exerciseName}</div>
                        <div className="text-sm text-muted-foreground">
                          <span data-testid={`next-sets-${index}`}>{next.setCount} sets</span>
                          {" x "}
                          <span data-testid={`next-reps-${index}`}>{next.targetReps} reps</span>
                          {" @ "}
                          <span data-testid={`next-weight-${index}`}>
                            {next.weight.toFixed(1)} {next.weightUnit === "Pounds" ? "lbs" : "kg"}
                          </span>
                          {next.hasAmrap && <span className="ml-1">(last set AMRAP)</span>}
                        </div>
                        {change && (
                          <div className={`text-xs mt-1 ${
                            getOutcomeLabel(change.change) === "SUCCESS" ? "text-green-600"
                              : getOutcomeLabel(change.change) === "FAILED" ? "text-red-600"
                              : "text-yellow-600"
                          }`}>
                            {change.change}
                          </div>
                        )}
                      </div>
                    );
                  })}
            </div>
          </Card>
        )}

        {/* New working weights to confirm next session */}
        {result.exercisesPendingWeightConfirmation?.length > 0 && (
          <Card className="p-6 mb-6 border-amber-400 bg-amber-50" data-testid="new-weights-card">
            <h2 className="text-xl font-bold mb-1 text-amber-700">New Weights Next Session</h2>
            <p className="text-sm text-muted-foreground mb-4">
              These exercises progressed. Cable/machine stacks vary between gyms, so use the closest
              weight your gym has and log what you actually lift — the app will adopt it automatically.
            </p>
            <div className="space-y-2">
              {result.exercisesPendingWeightConfirmation.map((ex) => (
                <div key={ex.exerciseId} className="flex items-center justify-between p-2 rounded bg-card border">
                  <span className="font-medium">{ex.exerciseName}</span>
                  <span className="text-sm font-semibold text-amber-700">
                    try {ex.suggestedWeight} {ex.weightUnit === "Pounds" ? "lbs" : "kg"}
                  </span>
                </div>
              ))}
            </div>
          </Card>
        )}

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
                <p className="text-sm text-muted-foreground mt-1">Send this workout to your Hevy app</p>
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
                <span className="text-sm px-2 py-1 bg-yellow-100 text-yellow-700 rounded">Deload Week</span>
              )}
            </div>
            <p className="text-sm text-muted-foreground mb-4">You've completed the week! Here's what's coming up next.</p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {Array.from({ length: workout.daysPerWeek }, (_, i) => i + 1).map((day) => {
                const dayExercises = workout.exercises.filter(e => e.assignedDay === day);
                return (
                  <div key={day} className="p-3 border rounded-lg bg-muted/20">
                    <h4 className="font-semibold mb-2">W{result.newCurrentWeek} D{day}</h4>
                    <div className="space-y-1">
                      {dayExercises.map(ex => (
                        <div key={ex.id} className="text-sm text-muted-foreground">{ex.name}</div>
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
          <Button variant="outline" onClick={onContinue} data-testid="back-to-workout-button">Back to Workout</Button>
          <Button onClick={onContinue} data-testid="continue-button">Continue</Button>
        </div>
      </main>
    </div>
  );
}
