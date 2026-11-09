import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useHevy } from "@/contexts/HevyContext";
import { pullWorkoutFromHevy } from "@/services/hevySyncService";
import toast from "react-hot-toast";
import { type WorkoutDto, type ExerciseDto, type ExerciseTemplate } from "@/types/workout";
import { EditExercisesModal } from "./EditExercisesModal";
import { ExerciseSubstitutionConfigModal } from "./ExerciseSubstitutionConfigModal";
import { type ProgressionConfig, type LinearConfig, type RepsPerSetConfig, type MinimalSetsConfig } from "./SubstitutionConfigForms";
import { useSubstituteExercise, useUpdateExercises, useUndoCompletion } from "@/hooks/useWorkouts";
import { hevyApi } from "@/services/hevyApi";
import { UndoConfirmationModal } from "@/components/shared/UndoConfirmationModal";
import { DayCard } from "./DayCard";
import { PUSH_AGAIN_HINT } from "./workoutConfigHandlers";

interface WeekOverviewProps {
  workout: WorkoutDto;
  onWorkoutUpdated?: () => void; // Callback to refetch workout after a change
}

export function WeekOverview({ workout, onWorkoutUpdated }: WeekOverviewProps) {
  const { isConfigured, isValid } = useHevy();
  const [editingDay, setEditingDay] = useState<number | null>(null);
  const [exerciseToSubstitute, setExerciseToSubstitute] = useState<ExerciseDto | null>(null);
  const navigate = useNavigate();
  const substituteExerciseMutation = useSubstituteExercise();
  const updateExercisesMutation = useUpdateExercises();
  const undoCompletionMutation = useUndoCompletion();
  const [showUndoModal, setShowUndoModal] = useState(false);

  // Group exercises by day
  const exercisesByDay = workout.exercises.reduce((acc, exercise) => {
    const day = exercise.assignedDay;
    if (!acc[day]) {
      acc[day] = [];
    }
    acc[day].push(exercise);
    return acc;
  }, {} as Record<number, ExerciseDto[]>);

  // Get days based on workout variant (4, 5, or 6 days)
  const daysPerWeek = workout.daysPerWeek || 4;
  const days = Array.from({ length: daysPerWeek }, (_, i) => i + 1);

  // Get completed days from workout data
  const completedDays = new Set(workout.completedDaysInCurrentWeek || []);
  const currentDay = workout.currentDay || 1;

  // Show the Hevy controls if a key is stored (even if validation is pending/null)
  const hevyEnabled = isConfigured && isValid !== false;

  const handlePullWorkout = async (dayNumber: number) => {
    try {
      toast.loading('Pulling workout from Hevy...', { id: 'pull-workout' });
      const result = await pullWorkoutFromHevy(workout, dayNumber);

      if (result.success) {
        const hasData = result.exercises?.length
          || result.substitutions?.length
          || result.weightDiscrepancies?.length
          || result.missingExercises?.length;

        if (hasData) {
          toast.success(result.message, { id: 'pull-workout' });
          // Navigate to workout session with all pulled data
          navigate(`/workout/session/${dayNumber}`, {
            state: {
              pulledData: result.exercises || [],
              pulledSubstitutions: result.substitutions || [],
              weightDiscrepancies: result.weightDiscrepancies || [],
              missingExercises: result.missingExercises || [],
            }
          });
        } else {
          toast.error(result.message || 'No workout data found', { id: 'pull-workout' });
        }
      } else {
        toast.error(result.message, { id: 'pull-workout' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to pull workout from Hevy';
      toast.error(message, { id: 'pull-workout' });
    }
  };

  // Exercise substitution handlers (for planning ahead)
  const handleOpenSubstitution = (exercise: ExerciseDto) => {
    setExerciseToSubstitute(exercise);
  };

  const handleUndoCompletion = async () => {
    try {
      await undoCompletionMutation.mutateAsync(workout.id);
      toast.success("Last workout undone successfully!");
      onWorkoutUpdated?.();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to undo workout";
      toast.error(message);
      throw error; // Re-throw so modal knows it failed
    }
  };

  const handleSubstituteWithConfig = async (
    originalExercise: ExerciseDto,
    substituteTemplate: ExerciseTemplate,
    progressionConfig: ProgressionConfig
  ) => {
    try {
      // Step 0: Look up the Hevy template ID for the new exercise
      let hevyTemplateId = substituteTemplate.name; // Fallback to name
      if (hevyApi.isConfigured()) {
        try {
          const hevyTemplates = await hevyApi.getAllExerciseTemplates();
          const matchingTemplate = hevyTemplates.find(
            t => t.title.toLowerCase() === substituteTemplate.name.toLowerCase()
          );
          if (matchingTemplate) {
            hevyTemplateId = matchingTemplate.id;
          } else {
            console.warn(`No Hevy template found for "${substituteTemplate.name}", using name as fallback`);
          }
        } catch (hevyError) {
          console.error('Failed to lookup Hevy template:', hevyError);
          // Continue with name as fallback
        }
      }

      // Step 1: Substitute the exercise name
      await substituteExerciseMutation.mutateAsync({
        workoutId: workout.id,
        request: {
          exerciseId: originalExercise.id,
          newExerciseName: substituteTemplate.name,
          newHevyExerciseTemplateId: hevyTemplateId,
          reason: "User substitution from week overview",
        },
      });

      // Step 2: Update the weight/training max based on progression type
      if (progressionConfig.type === "Linear") {
        const config = progressionConfig as LinearConfig;
        await updateExercisesMutation.mutateAsync({
          workoutId: workout.id,
          request: {
            updates: [{
              exerciseId: originalExercise.id,
              trainingMaxValue: config.trainingMaxValue,
              trainingMaxUnit: config.trainingMaxUnit,
              reason: "Updated during substitution",
            }],
          },
        });
      } else if (progressionConfig.type === "RepsPerSet") {
        const config = progressionConfig as RepsPerSetConfig;
        await updateExercisesMutation.mutateAsync({
          workoutId: workout.id,
          request: {
            updates: [{
              exerciseId: originalExercise.id,
              weightValue: config.startingWeight,
              weightUnit: config.weightUnit,
              reason: "Updated during substitution",
            }],
          },
        });
      } else if (progressionConfig.type === "MinimalSets") {
        const config = progressionConfig as MinimalSetsConfig;
        await updateExercisesMutation.mutateAsync({
          workoutId: workout.id,
          request: {
            updates: [{
              exerciseId: originalExercise.id,
              weightValue: config.weight,
              weightUnit: config.weightUnit,
              reason: "Updated during substitution",
            }],
          },
        });
      }

      toast.success(`Substituted ${originalExercise.name} with ${substituteTemplate.name}`);
      onWorkoutUpdated?.();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Failed to substitute exercise";
      toast.error(message);
    }
  };

  return (
    <Card className="p-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-bold">This Week's Training</h2>
        <div className="flex items-center gap-3">
          <div className="text-sm text-muted-foreground">
            Week {workout.currentWeek} of {workout.totalWeeks}
            {workout.isWeekComplete && (
              <span className="ml-2 text-success font-medium">✓ Week Complete</span>
            )}
          </div>
          {/* Undo Last Workout Button - only show if there are completed days */}
          {completedDays.size > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowUndoModal(true)}
              className="flex items-center gap-2 text-destructive border-destructive hover:bg-destructive/10"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
              </svg>
              Undo Last Workout
            </Button>
          )}
        </div>
      </div>

      {/* Routines are pushed by the server now; this is where the old Send buttons were. */}
      {hevyEnabled && (
        <div className="mb-4 text-sm text-muted-foreground" data-testid="hevy-push-pointer">
          <p>Sessions are sent to Hevy from Today and Settings.</p>
          <div className="flex flex-wrap gap-x-6">
            <Link to="/today" className="inline-flex min-h-11 items-center font-medium text-primary underline">
              Push to Hevy on Today
            </Link>
            <Link to="/settings" className="inline-flex min-h-11 items-center font-medium text-primary underline">
              Push this week in Settings
            </Link>
          </div>
        </div>
      )}

      {/* Progress indicator */}
      <div className="mb-4">
        <div className="flex items-center gap-2 mb-2">
          <span className="text-sm text-muted-foreground">Progress:</span>
          <span className="text-sm font-medium">
            {completedDays.size} / {daysPerWeek} days completed
          </span>
        </div>
        <div className="flex gap-1">
          {days.map((day) => (
            <div
              key={day}
              className={`h-2 flex-1 rounded ${
                completedDays.has(day)
                  ? "bg-success"
                  : day === currentDay
                  ? "bg-primary"
                  : "bg-muted"
              }`}
              title={`Day ${day}: ${completedDays.has(day) ? "Completed" : day === currentDay ? "Current" : "Pending"}`}
            />
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {days.map((day) => (
          <DayCard
            key={day}
            weekNumber={workout.currentWeek}
            dayNumber={day}
            exercises={exercisesByDay[day] || []}
            isCompleted={completedDays.has(day)}
            isCurrent={day === currentDay && !completedDays.has(day)}
            hevyEnabled={hevyEnabled}
            onEdit={() => setEditingDay(day)}
            onPullWorkout={() => handlePullWorkout(day)}
            onSubstituteExercise={handleOpenSubstitution}
            blockSequence={workout.blockSequence ?? [1, 2, 3]}
          />
        ))}
      </div>

      {/* Edit Exercises Modal */}
      <EditExercisesModal
        workout={workout}
        day={editingDay || 1}
        isOpen={editingDay !== null}
        onClose={() => setEditingDay(null)}
        onExercisesChanged={() => {
          onWorkoutUpdated?.();
          toast.success(hevyEnabled ? `Exercises updated. ${PUSH_AGAIN_HINT}` : 'Exercises updated.');
        }}
      />

      {/* Exercise Substitution Modal with Configuration */}
      {exerciseToSubstitute && (
        <ExerciseSubstitutionConfigModal
          exercise={exerciseToSubstitute}
          isOpen={exerciseToSubstitute !== null}
          onClose={() => setExerciseToSubstitute(null)}
          onSubstitute={handleSubstituteWithConfig}
        />
      )}

      {/* Undo Confirmation Modal */}
      <UndoConfirmationModal
        isOpen={showUndoModal}
        onClose={() => setShowUndoModal(false)}
        onConfirm={handleUndoCompletion}
        dayNumber={Math.max(...Array.from(completedDays), 1)}
        weekNumber={workout.currentWeek}
        wouldRollbackWeek={false}
      />
    </Card>
  );
}
