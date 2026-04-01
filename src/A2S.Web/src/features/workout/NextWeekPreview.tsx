import { useMemo, useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { getWeekParameters, getTemplateWeek, getBlockType, roundToGymIncrement, type WeekParameters } from "@/utils/weekParameters";
import { WeightUnit, type WorkoutDto, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto } from "@/types/workout";
import { useHevy } from "@/contexts/HevyContext";
import { syncWeekToHevy, syncDayAsRoutineForWeek, getOrCreateRoutineFolder } from "@/services/hevySyncService";
import { workoutsApi } from "@/api/workouts";
import toast from "react-hot-toast";

interface NextWeekPreviewProps {
  workout: WorkoutDto;
  onWorkoutUpdated?: () => void;
}

/**
 * Get days that have already been synced to Hevy for a specific week
 */
function getSyncedDaysForWeek(workout: WorkoutDto, weekNumber: number): Set<number> {
  const syncedDays = new Set<number>();
  const syncedRoutines = workout.hevySyncedRoutines || {};

  // Check each day to see if it's been synced for this week
  for (let day = 1; day <= (workout.daysPerWeek || 4); day++) {
    const key = `week${weekNumber}-day${day}`;
    if (syncedRoutines[key]) {
      syncedDays.add(day);
    }
  }

  return syncedDays;
}

export function NextWeekPreview({ workout, onWorkoutUpdated }: NextWeekPreviewProps) {
  const { isConfigured, isValid } = useHevy();
  const [isSyncingWeek, setIsSyncingWeek] = useState(false);
  const [sessionSyncedDays, setSessionSyncedDays] = useState<Set<number>>(new Set());

  const nextWeek = workout.currentWeek + 1;
  const isLastWeek = workout.currentWeek >= workout.totalWeeks;
  const daysPerWeek = workout.daysPerWeek || 4;
  const days = Array.from({ length: daysPerWeek }, (_, i) => i + 1);

  // Get completed days from current week
  const completedDaysInCurrentWeek = new Set(workout.completedDaysInCurrentWeek || []);

  // Group exercises by day
  const exercisesByDay = workout.exercises.reduce((acc, exercise) => {
    const day = exercise.assignedDay;
    if (!acc[day]) {
      acc[day] = [];
    }
    acc[day].push(exercise);
    return acc;
  }, {} as Record<number, ExerciseDto[]>);

  // Calculate next week parameters using block sequence translation
  const blockSequence = workout.blockSequence ?? [1, 2, 3];
  const templateWeek = nextWeek <= workout.totalWeeks ? getTemplateWeek(nextWeek, blockSequence) : nextWeek;
  const nextWeekParams = getWeekParameters(templateWeek);
  const nextBlockType = nextWeek <= workout.totalWeeks ? getBlockType(nextWeek, blockSequence) : 1;
  const nextBlockIndex = Math.floor((nextWeek - 1) / 7);

  // Show Hevy buttons if configured (even if validation is pending/null)
  const hevyEnabled = isConfigured && isValid !== false;

  // Get already synced days from the workout data (persisted in DB)
  const persistedSyncedDays = useMemo(() => getSyncedDaysForWeek(workout, nextWeek), [workout, nextWeek]);

  // Combine persisted and session synced days
  const syncedDays = useMemo(() => {
    return new Set([...persistedSyncedDays, ...sessionSyncedDays]);
  }, [persistedSyncedDays, sessionSyncedDays]);

  // Check if already fully synced for next week
  const isWeekFullySynced = syncedDays.size >= days.length;

  const handleSyncWeekToHevy = async () => {
    if (isWeekFullySynced) {
      toast.error('Next week has already been synced to Hevy');
      return;
    }

    setIsSyncingWeek(true);
    try {
      const result = await syncWeekToHevy(workout, nextWeek);
      if (result.success) {
        toast.success(result.message);
        // Mark all days as synced in this session
        setSessionSyncedDays(new Set(days));
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

      const result = await syncDayAsRoutineForWeek(workout, dayNumber, nextWeek, folderId);
      if (result.success) {
        toast.success(result.message);
        setSessionSyncedDays(prev => new Set([...prev, dayNumber]));
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

  if (isLastWeek) {
    return (
      <Card className="p-6 mt-6">
        <h2 className="text-xl font-bold mb-4">Next Week</h2>
        <div className="text-center py-8 text-muted-foreground">
          <p>You're on the final week of your program!</p>
          <p className="mt-2">Complete this week to finish your training cycle.</p>
        </div>
      </Card>
    );
  }

  return (
    <Card className="p-6 mt-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-bold">Next Week Preview</h2>
        <div className="flex items-center gap-3">
          <div className="text-sm text-muted-foreground">
            Week {nextWeek} of {workout.totalWeeks}
            {nextWeekParams.isDeload && (
              <span className="ml-2 text-yellow-500 font-medium">Deload Week</span>
            )}
          </div>
          {hevyEnabled && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleSyncWeekToHevy}
              disabled={isSyncingWeek || isWeekFullySynced}
              className="flex items-center gap-2"
              title={isWeekFullySynced ? `Week ${nextWeek} has already been synced to Hevy` : undefined}
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

      <div className="mb-4 text-sm text-muted-foreground">
        <span>Block {nextBlockIndex + 1}/{blockSequence.length} (Type {nextBlockType})</span>
        <span className="mx-2">•</span>
        <span>Intensity: {Math.round(nextWeekParams.intensity * 100)}%</span>
        <span className="mx-2">•</span>
        <span>{nextWeekParams.sets} sets × {nextWeekParams.targetReps} reps</span>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {days.map((day) => {
          // A day's preview is revealed when that day is completed in the current week
          const isRevealed = completedDaysInCurrentWeek.has(day);
          const exercises = exercisesByDay[day] || [];

          return (
            <NextWeekDayCard
              key={day}
              weekNumber={nextWeek}
              dayNumber={day}
              exercises={exercises}
              weekParams={nextWeekParams}
              isRevealed={isRevealed}
              hevyEnabled={hevyEnabled}
              isSynced={syncedDays.has(day)}
              onSyncToHevy={() => handleSyncDayToHevy(day)}
            />
          );
        })}
      </div>

      <p className="text-xs text-muted-foreground mt-4 text-center">
        Complete a day in the current week to reveal next week's training for that day
      </p>
    </Card>
  );
}

interface NextWeekDayCardProps {
  weekNumber: number;
  dayNumber: number;
  exercises: ExerciseDto[];
  weekParams: WeekParameters;
  isRevealed: boolean;
  hevyEnabled: boolean;
  isSynced: boolean;
  onSyncToHevy: () => void;
}

function NextWeekDayCard({ weekNumber, dayNumber, exercises, weekParams, isRevealed, hevyEnabled, isSynced, onSyncToHevy }: NextWeekDayCardProps) {
  const [isSyncing, setIsSyncing] = useState(false);

  const handleSync = async () => {
    setIsSyncing(true);
    try {
      await onSyncToHevy();
    } finally {
      setIsSyncing(false);
    }
  };

  return (
    <div
      className={`p-4 border rounded-lg transition-all relative ${
        isRevealed
          ? "border-border bg-card"
          : "border-border/50 bg-muted/20"
      }`}
    >
      {/* Blur overlay for unrevealed days */}
      {!isRevealed && (
        <div className="absolute inset-0 backdrop-blur-sm bg-background/60 rounded-lg flex items-center justify-center z-10">
          <div className="text-center">
            <svg className="w-8 h-8 mx-auto mb-2 text-muted-foreground/50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
            </svg>
            <p className="text-xs text-muted-foreground">
              Complete W{weekNumber - 1} D{dayNumber}
            </p>
          </div>
        </div>
      )}

      <div className="flex items-center justify-between mb-3">
        <div>
          <h3 className="font-semibold">W{weekNumber} D{dayNumber}</h3>
          <span className="text-xs text-muted-foreground">
            {exercises.length} exercise{exercises.length !== 1 ? 's' : ''}
          </span>
        </div>
      </div>

      <div className={`space-y-2 ${!isRevealed ? 'opacity-30' : ''}`}>
        {exercises.length > 0 ? (
          exercises
            .sort((a, b) => a.orderInDay - b.orderInDay)
            .map((exercise) => (
              <NextWeekExercisePreview
                key={exercise.id}
                exercise={exercise}
                weekParams={weekParams}
              />
            ))
        ) : (
          <p className="text-sm text-muted-foreground">No exercises</p>
        )}
      </div>

      {/* Send to Hevy button - only visible for revealed days */}
      {hevyEnabled && isRevealed && (
        <div className="mt-3">
          <Button
            variant="ghost"
            size="sm"
            className="w-full text-xs"
            onClick={handleSync}
            disabled={isSyncing || isSynced}
          >
            {isSyncing ? (
              <>
                <svg className="h-3 w-3 mr-1 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                </svg>
                Syncing...
              </>
            ) : isSynced ? (
              <span className="flex items-center gap-1">
                <svg className="h-3 w-3" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
                Sent to Hevy
              </span>
            ) : (
              <>
                <svg className="h-3 w-3 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                </svg>
                Send to Hevy
              </>
            )}
          </Button>
        </div>
      )}
    </div>
  );
}

interface NextWeekExercisePreviewProps {
  exercise: ExerciseDto;
  weekParams: WeekParameters;
}

function NextWeekExercisePreview({ exercise, weekParams }: NextWeekExercisePreviewProps) {
  const isLinear = exercise.progression.type === "Linear";
  const isRepsPerSet = exercise.progression.type === "RepsPerSet";
  const isMinimalSets = exercise.progression.type === "MinimalSets";
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
  const minimalSetsProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

  // Calculate next week's working weight for linear exercises
  const nextWeekWeight = useMemo(() => {
    if (linearProg) {
      const tmValue = linearProg.trainingMax.value;
      const workingWeight = roundToGymIncrement(tmValue * weekParams.intensity);
      return workingWeight;
    }
    return null;
  }, [linearProg, weekParams.intensity]);

  return (
    <div className="border-l-2 border-primary/20 pl-2 py-1">
      <div className="text-sm font-medium truncate">{exercise.name}</div>

      {linearProg && nextWeekWeight && (
        <div className="text-xs text-muted-foreground">
          {nextWeekWeight} {linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs"} × {weekParams.sets} × {weekParams.targetReps}
          {linearProg.useAmrap && <span className="ml-1 text-primary">+AMRAP</span>}
        </div>
      )}

      {repsPerSetProg && (
        <div className="text-xs text-muted-foreground">
          {repsPerSetProg.currentWeight} {repsPerSetProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"} × {repsPerSetProg.currentSetCount} × {repsPerSetProg.repRange?.minimum ?? 0}-{repsPerSetProg.repRange?.maximum ?? 0}
        </div>
      )}

      {minimalSetsProg && (
        <div className="text-xs text-muted-foreground">
          {minimalSetsProg.currentWeight} {minimalSetsProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"} × ~{minimalSetsProg.currentSetCount} sets
        </div>
      )}
    </div>
  );
}
