import { useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useHevy } from "@/contexts/HevyContext";
import { syncDayAsRoutine, syncWorkoutToHevy, pullWorkoutFromHevy, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { workoutsApi } from "@/api/workouts";
import toast from "react-hot-toast";
import { type WorkoutDto, type ExerciseDto, type ExerciseTemplate } from "@/types/workout";
import { EditExercisesModal } from "./EditExercisesModal";
import { ExerciseSubstitutionConfigModal } from "./ExerciseSubstitutionConfigModal";
import { type ProgressionConfig, type LinearConfig, type RepsPerSetConfig, type MinimalSetsConfig } from "./SubstitutionConfigForms";
import { useSubstituteExercise, useUpdateExercises, useUndoCompletion } from "@/hooks/useWorkouts";
import { hevyApi } from "@/services/hevyApi";
import { UndoConfirmationModal } from "@/components/shared/UndoConfirmationModal";
import { DayCard } from "./DayCard";

interface WeekOverviewProps {
  workout: WorkoutDto;
  onWorkoutUpdated?: () => void; // Callback to refetch workout after sync
}

/**
 * Get days that have already been synced to Hevy for the current week
 */
function getSyncedDaysForCurrentWeek(workout: WorkoutDto): Set<number> {
  const syncedDays = new Set<number>();
  const syncedRoutines = workout.hevySyncedRoutines || {};
  const currentWeek = workout.currentWeek;

  // Check each day to see if it's been synced for this week
  for (let day = 1; day <= (workout.daysPerWeek || 4); day++) {
    const key = `week${currentWeek}-day${day}`;
    if (syncedRoutines[key]) {
      syncedDays.add(day);
    }
  }

  return syncedDays;
}

export function WeekOverview({ workout, onWorkoutUpdated }: WeekOverviewProps) {
  const { isConfigured, isValid } = useHevy();
  const [isSyncingWeek, setIsSyncingWeek] = useState(false);
  const [editingDay, setEditingDay] = useState<number | null>(null);
  const [exerciseToSubstitute, setExerciseToSubstitute] = useState<ExerciseDto | null>(null);
  const navigate = useNavigate();
  const substituteExerciseMutation = useSubstituteExercise();
  const updateExercisesMutation = useUpdateExercises();
  const undoCompletionMutation = useUndoCompletion();
  const [showUndoModal, setShowUndoModal] = useState(false);

  // Get already synced days from the workout data (persisted in DB)
  const persistedSyncedDays = useMemo(() => getSyncedDaysForCurrentWeek(workout), [workout]);

  // Track additional syncs from this session (will be merged with persisted)
  const [sessionSyncedDays, setSessionSyncedDays] = useState<Set<number>>(new Set());

  // Track sync timestamps for this session (day -> timestamp)
  const [syncTimestamps, setSyncTimestamps] = useState<Record<number, Date>>({});

  // Combine persisted and session synced days
  const syncedDays = useMemo(() => {
    return new Set([...persistedSyncedDays, ...sessionSyncedDays]);
  }, [persistedSyncedDays, sessionSyncedDays]);

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

  // Show Hevy buttons if configured (even if validation is pending/null)
  const hevyEnabled = isConfigured && isValid !== false;

  // Check if already fully synced for this week
  const isWeekFullySynced = syncedDays.size >= days.length;

  const handleSyncWeekToHevy = async () => {
    if (isWeekFullySynced) {
      toast.error('This week has already been synced to Hevy');
      return;
    }

    setIsSyncingWeek(true);
    try {
      const result = await syncWorkoutToHevy(workout);
      if (result.success) {
        toast.success(result.message);
        // Mark all days as synced in this session with timestamps
        const now = new Date();
        setSessionSyncedDays(new Set(days));
        const timestamps: Record<number, Date> = {};
        days.forEach(d => { timestamps[d] = now; });
        setSyncTimestamps(prev => ({ ...prev, ...timestamps }));
        // Trigger refetch to update workout with new hevySyncedRoutines
        onWorkoutUpdated?.();
      } else {
        toast.error(result.message);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to sync week to Hevy';
      toast.error(message);
    } finally {
      setIsSyncingWeek(false);
    }
  };

  const handleSyncDayToHevy = async (dayNumber: number) => {
    if (syncedDays.has(dayNumber)) {
      toast.error(`Day ${dayNumber} has already been synced to Hevy`);
      return;
    }

    try {
      // Ensure the routine folder exists before syncing the day
      let folderId = workout.hevyRoutineFolderId;
      if (!folderId) {
        const folderResult = await getOrCreateRoutineFolder(workout.name);
        if (folderResult) {
          folderId = folderResult.folderId;
          // Persist folder ID to workout in database
          try {
            await workoutsApi.setHevyFolderId(workout.id, folderId);
          } catch (err) {
            console.error('Failed to save folder ID:', err);
            // Continue anyway - folder was created in Hevy
          }
        }
      }

      const result = await syncDayAsRoutine(workout, dayNumber, folderId);
      if (result.success) {
        toast.success(result.message);
        setSessionSyncedDays(prev => new Set([...prev, dayNumber]));
        setSyncTimestamps(prev => ({ ...prev, [dayNumber]: new Date() }));
        // Trigger refetch to update workout with new hevySyncedRoutines
        onWorkoutUpdated?.();
      } else {
        toast.error(result.message);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to sync day to Hevy';
      toast.error(message);
    }
  };

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
      // Clear session synced days since exercises changed
      setSessionSyncedDays(new Set());
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
          {hevyEnabled && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleSyncWeekToHevy}
              disabled={isSyncingWeek || isWeekFullySynced}
              className="flex items-center gap-2"
              title={isWeekFullySynced ? `Week ${workout.currentWeek} has already been synced to Hevy` : undefined}
            >
              {isSyncingWeek ? (
                <>
                  <svg className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Syncing...
                </>
              ) : isWeekFullySynced ? (
                <>
                  <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                  Week Synced
                </>
              ) : (
                <>
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                  </svg>
                  Send Week to Hevy
                </>
              )}
            </Button>
          )}
        </div>
      </div>

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
            isSynced={syncedDays.has(day)}
            syncTimestamp={syncTimestamps[day]}
            onSyncToHevy={() => handleSyncDayToHevy(day)}
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
        onSyncRequired={() => {
          // Clear session synced days when exercises are edited - they need to be re-synced
          setSessionSyncedDays(new Set());
          onWorkoutUpdated?.();
          toast.success('Exercises updated! Re-sync to Hevy to apply changes.');
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
