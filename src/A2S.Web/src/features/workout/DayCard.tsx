import { useState } from "react";
import { Button } from "@/components/ui/button";
import { WeightUnit, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto } from "@/types/workout";
import { getWeekParameters, getTemplateWeek, roundToGymIncrement } from "@/utils/weekParameters";

export interface DayCardProps {
  weekNumber: number;
  dayNumber: number;
  exercises: ExerciseDto[];
  isCompleted: boolean;
  isCurrent: boolean;
  hevyEnabled: boolean;
  isSynced: boolean;
  syncTimestamp?: Date;
  onSyncToHevy: () => void;
  onEdit: () => void;
  onPullWorkout: () => void;
  onSubstituteExercise: (exercise: ExerciseDto) => void;
  blockSequence: number[];
}

function formatSyncTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;

  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  }) + ', ' + date.toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

export function ExerciseDetailCard({ exercise, weekNumber, blockSequence, onSubstitute }: { exercise: ExerciseDto; weekNumber: number; blockSequence: number[]; onSubstitute: () => void }) {
  const isLinear = exercise.progression.type === "Linear";
  const isRepsPerSet = exercise.progression.type === "RepsPerSet";
  const isMinimalSets = exercise.progression.type === "MinimalSets";
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
  const minimalSetsProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

  const templateWeek = getTemplateWeek(weekNumber, blockSequence);
  const weekParams = getWeekParameters(templateWeek);

  return (
    <div className="border-l-2 border-primary/30 pl-3 py-1 group">
      <div className="flex items-center justify-between">
        <div className="font-semibold text-sm">{exercise.name}</div>
        <button
          onClick={onSubstitute}
          className="p-1 opacity-0 group-hover:opacity-100 hover:bg-muted rounded transition-all"
          title="Substitute exercise"
        >
          <svg className="w-3.5 h-3.5 text-muted-foreground hover:text-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
          </svg>
        </button>
      </div>

      {linearProg && (() => {
        const unitLabel = linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs";
        const unitKey = linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" as const : "lbs" as const;
        const workingWeight = roundToGymIncrement(linearProg.trainingMax.value * weekParams.intensity, unitKey);
        return (
          <div className="text-xs text-muted-foreground mt-1 space-y-0.5">
            <div className="flex justify-between">
              <span>Weight:</span>
              <span className="font-medium text-foreground">{workingWeight} {unitLabel}</span>
            </div>
            <div className="flex justify-between">
              <span>Sets × Reps:</span>
              <span className="font-medium text-foreground">{weekParams.sets} × {weekParams.targetReps}</span>
            </div>
            {linearProg.useAmrap && !weekParams.isDeload && (
              <div className="flex justify-between">
                <span>AMRAP Target:</span>
                <span className="font-medium text-primary">{weekParams.repOutTarget}+</span>
              </div>
            )}
            <div className="flex justify-between text-[10px]">
              <span>TM:</span>
              <span>{linearProg.trainingMax.value} {unitLabel} @ {Math.round(weekParams.intensity * 100)}%</span>
            </div>
          </div>
        );
      })()}

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
            <span className="font-medium text-foreground">{repsPerSetProg.currentSetCount}</span>
          </div>
          <div className="flex justify-between">
            <span>Reps:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.repRange?.minimum ?? 0}-{repsPerSetProg.repRange?.maximum ?? 0}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Target Sets:</span>
            <span className="font-medium text-foreground">{repsPerSetProg.targetSets}</span>
          </div>
          {repsPerSetProg.isUnilateral && (
            <div className="flex justify-between items-center pt-1 mt-1 border-t border-border/50">
              <span>Unilateral:</span>
              <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-primary text-primary-foreground">
                Per Side
              </span>
            </div>
          )}
          {repsPerSetProg.isUnilateral && (
            <div className="text-primary text-[10px] font-medium">
              {repsPerSetProg.currentSetCount} sets × 2 sides = {repsPerSetProg.currentSetCount * 2} total
            </div>
          )}
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
            <span className="font-medium text-foreground">{minimalSetsProg.targetTotalReps}</span>
          </div>
        </div>
      )}
    </div>
  );
}

export function DayCard({ weekNumber, dayNumber, exercises, isCompleted, isCurrent, hevyEnabled, isSynced, syncTimestamp, onSyncToHevy, onEdit, onPullWorkout, onSubstituteExercise, blockSequence }: DayCardProps) {
  const [isSyncing, setIsSyncing] = useState(false);
  const [isPulling, setIsPulling] = useState(false);
  const dayLabel = `W${weekNumber} D${dayNumber}`;
  const isUpcoming = !isCompleted && !isCurrent;

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
          ? "border-success bg-success/10"
          : isCurrent
          ? "border-primary bg-primary/5 ring-2 ring-primary/20"
          : isUpcoming
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
          {isUpcoming && (
            <span className="text-xs text-muted-foreground">Upcoming</span>
          )}
        </div>
        <div className="flex items-center gap-2">
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
            <svg className="w-5 h-5 text-success" fill="currentColor" viewBox="0 0 20 20" data-testid={`day-${dayNumber}-completed-icon`}>
              <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
            </svg>
          )}
          {isUpcoming && (
            <svg className="w-5 h-5 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
            </svg>
          )}
        </div>
      </div>

      <div className="divide-y divide-border/40 mb-4">
        {exercises.length > 0 ? (
          exercises
            .sort((a, b) => a.orderInDay - b.orderInDay)
            .map((exercise) => (
              <div key={exercise.id} className="py-2 first:pt-0 last:pb-0">
                <ExerciseDetailCard
                  exercise={exercise}
                  weekNumber={weekNumber}
                  blockSequence={blockSequence}
                  onSubstitute={() => onSubstituteExercise(exercise)}
                />
              </div>
            ))
        ) : (
          <p className="text-sm text-muted-foreground">No exercises assigned</p>
        )}
      </div>

      <div className="space-y-2">
        {isCompleted ? (
          <Button variant="outline" size="sm" className="w-full" disabled data-testid={`start-workout-day-${dayNumber}`}>
            Completed
          </Button>
        ) : isUpcoming ? (
          <Button variant="outline" size="sm" className="w-full" disabled data-testid={`start-workout-day-${dayNumber}`}>
            Upcoming
          </Button>
        ) : hevyEnabled && (
          <div className="flex flex-col gap-2">
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              onClick={handleSync}
              disabled={isSyncing || isSynced}
              data-testid={`send-to-hevy-day-${dayNumber}`}
            >
              {isSyncing ? (
                <>
                  <svg className="h-4 w-4 mr-1 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Sending...
                </>
              ) : isSynced ? (
                <span className="flex items-center gap-1" title={syncTimestamp ? `Synced ${syncTimestamp.toLocaleString()}` : 'Synced to Hevy'}>
                  <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                  {syncTimestamp ? `Synced ${formatSyncTime(syncTimestamp)}` : 'Sent to Hevy'}
                </span>
              ) : (
                <>
                  <svg className="h-4 w-4 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                  </svg>
                  Send to Hevy
                </>
              )}
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              onClick={handlePull}
              disabled={isPulling}
              title="Pull workout data from Hevy"
              data-testid={`pull-workout-day-${dayNumber}`}
            >
              {isPulling ? (
                <>
                  <svg className="h-4 w-4 mr-1 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Pulling...
                </>
              ) : (
                <>
                  <svg className="h-4 w-4 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M9 19l3 3m0 0l3-3m-3 3V10" />
                  </svg>
                  Pull from Hevy
                </>
              )}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
