import { useMemo } from "react";
import { Card } from "@/components/ui/card";
import { getWeekParameters, roundToGymIncrement, type WeekParameters } from "@/utils/weekParameters";
import { WeightUnit, type WorkoutDto, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto } from "@/types/workout";

interface NextWeekPreviewProps {
  workout: WorkoutDto;
}

export function NextWeekPreview({ workout }: NextWeekPreviewProps) {
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

  // Calculate next week parameters
  const nextWeekParams = getWeekParameters(nextWeek);
  const currentBlock = Math.ceil(nextWeek / 7);

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
        <div className="text-sm text-muted-foreground">
          Week {nextWeek} of {workout.totalWeeks}
          {nextWeekParams.isDeload && (
            <span className="ml-2 text-yellow-500 font-medium">Deload Week</span>
          )}
        </div>
      </div>

      <div className="mb-4 text-sm text-muted-foreground">
        <span>Block {currentBlock}</span>
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
}

function NextWeekDayCard({ weekNumber, dayNumber, exercises, weekParams, isRevealed }: NextWeekDayCardProps) {
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
          {repsPerSetProg.currentWeight} {repsPerSetProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"} × {repsPerSetProg.currentSetCount} × {repsPerSetProg.repRange?.target ?? 0}
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
