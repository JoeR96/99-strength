import { useState, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useHevy } from "@/contexts/HevyContext";
import { syncDayAsRoutine, syncWorkoutToHevy, pullWorkoutFromHevy } from "@/services/hevySyncService";
import toast from "react-hot-toast";
import { WeightUnit, type WorkoutDto, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto } from "@/types/workout";
import { EditExercisesModal } from "./EditExercisesModal";

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
  const navigate = useNavigate();

  // Get already synced days from the workout data (persisted in DB)
  const persistedSyncedDays = useMemo(() => getSyncedDaysForCurrentWeek(workout), [workout]);

  // Track additional syncs from this session (will be merged with persisted)
  const [sessionSyncedDays, setSessionSyncedDays] = useState<Set<number>>(new Set());

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
      const result = await syncDayAsRoutine(workout, dayNumber);
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

  const handlePullWorkout = async (dayNumber: number) => {
    try {
      toast.loading('Pulling workout from Hevy...', { id: 'pull-workout' });
      const result = await pullWorkoutFromHevy(workout, dayNumber);

      if (result.success && (result.exercises || result.substitutions)) {
        toast.success(result.message, { id: 'pull-workout' });
        // Navigate to workout session with pulled data and substitutions
        navigate(`/workout/session/${dayNumber}`, {
          state: {
            pulledData: result.exercises || [],
            pulledSubstitutions: result.substitutions || [],
          }
        });
      } else {
        toast.error(result.message, { id: 'pull-workout' });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to pull workout from Hevy';
      toast.error(message, { id: 'pull-workout' });
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
              <span className="ml-2 text-green-500 font-medium">✓ Week Complete</span>
            )}
          </div>
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
                  ? "bg-green-500"
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
            onSyncToHevy={() => handleSyncDayToHevy(day)}
            onEdit={() => setEditingDay(day)}
            onPullWorkout={() => handlePullWorkout(day)}
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
    </Card>
  );
}

interface DayCardProps {
  weekNumber: number;
  dayNumber: number;
  exercises: ExerciseDto[];
  isCompleted: boolean;
  isCurrent: boolean;
  hevyEnabled: boolean;
  isSynced: boolean;
  onSyncToHevy: () => void;
  onEdit: () => void;
  onPullWorkout: () => void;
}

function DayCard({ weekNumber, dayNumber, exercises, isCompleted, isCurrent, hevyEnabled, isSynced, onSyncToHevy, onEdit, onPullWorkout }: DayCardProps) {
  const [isSyncing, setIsSyncing] = useState(false);
  const [isPulling, setIsPulling] = useState(false);
  // Use Week/Day format instead of weekday names
  const dayLabel = `W${weekNumber} D${dayNumber}`;

  // Day is locked if it's not completed and not current
  const isLocked = !isCompleted && !isCurrent;

  const handleSync = async () => {
    setIsSyncing(true);
    try {
      await onSyncToHevy();
    } finally {
      setIsSyncing(false);
    }
  };

  const handlePull = async () => {
    setIsPulling(true);
    try {
      await onPullWorkout();
    } finally {
      setIsPulling(false);
    }
  };

  return (
    <div
      className={`p-4 border rounded-lg transition-all ${
        isCompleted
          ? "border-green-500 bg-green-50 dark:bg-green-950"
          : isCurrent
          ? "border-primary bg-primary/5 ring-2 ring-primary/20"
          : isLocked
          ? "border-border bg-muted/30 opacity-60"
          : "border-border"
      }`}
      data-testid={`day-card-${dayNumber}`}
    >
      <div className="flex items-center justify-between mb-3">
        <div>
          <h3 className="font-semibold">{dayLabel}</h3>
          {isCurrent && !isCompleted && (
            <span className="text-xs text-primary font-medium">Current</span>
          )}
          {isLocked && (
            <span className="text-xs text-muted-foreground">Locked</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {/* Edit button */}
          <button
            onClick={onEdit}
            className="p-1 hover:bg-muted rounded transition-colors"
            title="Edit exercises"
          >
            <svg className="w-4 h-4 text-muted-foreground hover:text-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
          {isCompleted && (
            <svg
              className="w-5 h-5 text-green-500"
              fill="currentColor"
              viewBox="0 0 20 20"
              data-testid={`day-${dayNumber}-completed-icon`}
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                clipRule="evenodd"
              />
            </svg>
          )}
          {isLocked && (
            <svg
              className="w-5 h-5 text-muted-foreground"
              fill="currentColor"
              viewBox="0 0 20 20"
            >
              <path
                fillRule="evenodd"
                d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z"
                clipRule="evenodd"
              />
            </svg>
          )}
        </div>
      </div>

      <div className="space-y-3 mb-4">
        {exercises.length > 0 ? (
          exercises
            .sort((a, b) => a.orderInDay - b.orderInDay)
            .map((exercise) => (
              <ExerciseDetailCard key={exercise.id} exercise={exercise} />
            ))
        ) : (
          <p className="text-sm text-muted-foreground">No exercises assigned</p>
        )}
      </div>

      <div className="space-y-2">
        {isCompleted ? (
          <Button
            variant="outline"
            size="sm"
            className="w-full"
            disabled
            data-testid={`start-workout-day-${dayNumber}`}
          >
            Completed
          </Button>
        ) : isLocked ? (
          <Button
            variant="outline"
            size="sm"
            className="w-full"
            disabled
            data-testid={`start-workout-day-${dayNumber}`}
          >
            Locked
          </Button>
        ) : (
          <div className="flex gap-2">
            <Link to={`/workout/session/${dayNumber}`} className="flex-1">
              <Button
                variant={isCurrent ? "default" : "outline"}
                size="sm"
                className="w-full"
                data-testid={`start-workout-day-${dayNumber}`}
              >
                {isCurrent ? "Start" : "Start"}
              </Button>
            </Link>
            {hevyEnabled && (
              <Button
                variant="outline"
                size="sm"
                onClick={handlePull}
                disabled={isPulling}
                title="Pull workout data from Hevy"
                data-testid={`pull-workout-day-${dayNumber}`}
              >
                {isPulling ? (
                  <svg className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                ) : (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M9 19l3 3m0 0l3-3m-3 3V10" />
                  </svg>
                )}
              </Button>
            )}
          </div>
        )}

        {/* Send to Hevy button - visible for all days including locked */}
        {hevyEnabled && (
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
              <>
                <svg className="h-3 w-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
                Sent to Hevy
              </>
            ) : (
              <>
                <svg className="h-3 w-3 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                </svg>
                Send to Hevy
              </>
            )}
          </Button>
        )}
      </div>
    </div>
  );
}

function ExerciseDetailCard({ exercise }: { exercise: ExerciseDto }) {
  const isLinear = exercise.progression.type === "Linear";
  const isRepsPerSet = exercise.progression.type === "RepsPerSet";
  const isMinimalSets = exercise.progression.type === "MinimalSets";
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
  const minimalSetsProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

  return (
    <div className="border-l-2 border-primary/30 pl-3 py-1">
      <div className="font-medium text-sm">{exercise.name}</div>

      {linearProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>TM:</span>
            <span className="font-medium text-foreground">
              {linearProg.trainingMax.value} {linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs"}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">
              {linearProg.baseSetsPerExercise}{linearProg.useAmrap ? " + AMRAP" : ""}
            </span>
          </div>
          {linearProg.useAmrap && (
            <div className="text-primary text-[10px] font-medium">
              AMRAP on last set
            </div>
          )}
        </div>
      )}

      {repsPerSetProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>Weight:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.currentWeight} {repsPerSetProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.currentSetCount}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Reps:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.repRange?.target ?? 0}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Range:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.repRange?.minimum ?? 0}-{repsPerSetProg.repRange?.maximum ?? 0}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Target Sets:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.targetSets}
            </span>
          </div>
        </div>
      )}

      {minimalSetsProg && (
        <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
          <div className="flex justify-between">
            <span>Weight:</span>
            <span className="font-medium text-foreground">
              {minimalSetsProg.currentWeight} {minimalSetsProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Sets:</span>
            <span className="font-medium text-foreground">
              {minimalSetsProg.currentSetCount} ({minimalSetsProg.minimumSets}-{minimalSetsProg.maximumSets})
            </span>
          </div>
          <div className="flex justify-between">
            <span>Target Reps:</span>
            <span className="font-medium text-foreground">
              {minimalSetsProg.targetTotalReps}
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
