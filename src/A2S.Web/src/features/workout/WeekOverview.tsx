import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { WeightUnit, type WorkoutDto, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto } from "@/types/workout";

interface WeekOverviewProps {
  workout: WorkoutDto;
}

export function WeekOverview({ workout }: WeekOverviewProps) {
  // Group exercises by day
  const exercisesByDay = workout.exercises.reduce((acc, exercise) => {
    const day = exercise.assignedDay;
    if (!acc[day]) {
      acc[day] = [];
    }
    acc[day].push(exercise);
    return acc;
  }, {} as Record<number, ExerciseDto[]>);

  // Get unique days (sorted)
  const days = Object.keys(exercisesByDay)
    .map(Number)
    .sort((a, b) => a - b);

  return (
    <Card className="p-6">
      <h2 className="text-xl font-bold mb-4">This Week's Training</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {days.map((day) => (
          <DayCard
            key={day}
            dayNumber={day}
            exercises={exercisesByDay[day]}
            isCompleted={false} // TODO: Track completion status
          />
        ))}
      </div>
    </Card>
  );
}

interface DayCardProps {
  dayNumber: number;
  exercises: ExerciseDto[];
  isCompleted: boolean;
}

function DayCard({ dayNumber, exercises, isCompleted }: DayCardProps) {
  const dayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const dayName = dayNames[dayNumber - 1] || `Day ${dayNumber}`;

  return (
    <div
      className={`p-4 border rounded-lg ${
        isCompleted
          ? "border-green-500 bg-green-50 dark:bg-green-950"
          : "border-border"
      }`}
    >
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-semibold">{dayName}</h3>
        {isCompleted && (
          <svg
            className="w-5 h-5 text-green-500"
            fill="currentColor"
            viewBox="0 0 20 20"
          >
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
              clipRule="evenodd"
            />
          </svg>
        )}
      </div>

      <div className="space-y-3 mb-4">
        {exercises
          .sort((a, b) => a.orderInDay - b.orderInDay)
          .map((exercise) => (
            <ExerciseDetailCard key={exercise.id} exercise={exercise} />
          ))}
      </div>

      <Button
        variant={isCompleted ? "outline" : "default"}
        size="sm"
        className="w-full"
        disabled={isCompleted}
      >
        {isCompleted ? "Completed" : "Start Workout"}
      </Button>
    </div>
  );
}

function ExerciseDetailCard({ exercise }: { exercise: ExerciseDto }) {
  const isLinear = exercise.progression.type === "Linear";
  const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
  const repsPerSetProg = !isLinear ? (exercise.progression as RepsPerSetProgressionDto) : null;

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
              {repsPerSetProg.repRange.target}
            </span>
          </div>
          <div className="flex justify-between">
            <span>Range:</span>
            <span className="font-medium text-foreground">
              {repsPerSetProg.repRange.minimum}-{repsPerSetProg.repRange.maximum}
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
    </div>
  );
}
