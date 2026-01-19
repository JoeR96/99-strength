import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import type { WorkoutDto } from "@/types/workout";

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
  }, {} as Record<number, typeof workout.exercises>);

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
  exercises: any[];
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

      <div className="space-y-2 mb-4">
        {exercises
          .sort((a, b) => a.orderInDay - b.orderInDay)
          .map((exercise) => (
            <div key={exercise.id} className="text-sm">
              <div className="font-medium">{exercise.name}</div>
              {exercise.progression.type === "Linear" && (
                <div className="text-xs text-muted-foreground">
                  {exercise.progression.baseSetsPerExercise} sets
                  {exercise.progression.useAmrap && " + AMRAP"}
                </div>
              )}
              {exercise.progression.type === "RepsPerSet" && (
                <div className="text-xs text-muted-foreground">
                  {exercise.progression.currentSets} sets ×{" "}
                  {exercise.progression.repRange.target} reps
                </div>
              )}
            </div>
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
