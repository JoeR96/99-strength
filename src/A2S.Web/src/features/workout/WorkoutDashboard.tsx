import { useCurrentWorkout } from "@/hooks/useWorkouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { WeekOverview } from "./WeekOverview";
import { useNavigate } from "react-router-dom";
import { WeightUnit, type LinearProgressionDto } from "@/types/workout";

export function WorkoutDashboard() {
  const { data: workout, isLoading, error } = useCurrentWorkout();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="max-w-6xl mx-auto p-6">
        <div className="text-center py-12">
          <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          <p className="mt-4 text-muted-foreground">Loading your workout...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-6xl mx-auto p-6">
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load workout</p>
          <Button className="mt-4" onClick={() => window.location.reload()}>
            Retry
          </Button>
        </Card>
      </div>
    );
  }

  if (!workout) {
    return (
      <div className="max-w-6xl mx-auto p-6">
        <Card className="p-8 text-center">
          <h2 className="text-2xl font-bold mb-2">No Active Workout</h2>
          <p className="text-muted-foreground mb-6">
            You don't have an active workout program yet. Let's create one!
          </p>
          <Button onClick={() => navigate("/setup")}>Create Workout Program</Button>
        </Card>
      </div>
    );
  }

  const currentBlock = Math.ceil(workout.currentWeek / 7);
  const weekInBlock = ((workout.currentWeek - 1) % 7) + 1;

  return (
    <div className="max-w-6xl mx-auto p-6">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">{workout.name}</h1>
        <div className="flex items-center gap-4 text-sm text-muted-foreground">
          <span>
            Week {workout.currentWeek} of {workout.totalWeeks}
          </span>
          <span>•</span>
          <span>
            Block {currentBlock}, Week {weekInBlock}
          </span>
          <span>•</span>
          <span>{workout.variant}-Day Program</span>
        </div>
      </div>

      {/* Progress bar */}
      <Card className="p-6 mb-6">
        <div className="flex items-center justify-between mb-2">
          <span className="font-semibold">Program Progress</span>
          <span className="text-sm text-muted-foreground">
            {Math.round((workout.currentWeek / workout.totalWeeks) * 100)}%
          </span>
        </div>
        <div className="h-2 bg-muted rounded-full overflow-hidden">
          <div
            className="h-full bg-primary transition-all duration-300"
            style={{
              width: `${(workout.currentWeek / workout.totalWeeks) * 100}%`,
            }}
          />
        </div>
      </Card>

      {/* Week Overview */}
      <WeekOverview workout={workout} />

      {/* Exercises Summary */}
      <Card className="mt-6 p-6">
        <h2 className="text-xl font-bold mb-4">Your Exercises</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {workout.exercises.map((exercise) => (
            <div key={exercise.id} className="p-4 border rounded-md">
              <div className="font-medium">{exercise.name}</div>
              <div className="text-sm text-muted-foreground mt-1">
                Day {exercise.assignedDay}
              </div>
              {exercise.progression.type === "Linear" && (() => {
                const linearProg = exercise.progression as LinearProgressionDto;
                return (
                  <div className="text-sm mt-2">
                    TM: {linearProg.trainingMax.value}
                    {linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs"}
                  </div>
                );
              })()}
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
