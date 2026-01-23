import { useCurrentWorkout } from "@/hooks/useWorkouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { WeekOverview } from "./WeekOverview";
import { useNavigate } from "react-router-dom";
import { Navbar } from "@/components/layout/Navbar";
import { WeightUnit, type LinearProgressionDto, type RepsPerSetProgressionDto } from "@/types/workout";

export function WorkoutDashboard() {
  const { data: workout, isLoading, error } = useCurrentWorkout();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="max-w-6xl mx-auto p-6">
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            <p className="mt-4 text-muted-foreground">Loading your workout...</p>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="max-w-6xl mx-auto p-6">
          <Card className="p-8 text-center">
            <p className="text-destructive">Failed to load workout</p>
            <Button className="mt-4" onClick={() => window.location.reload()}>
              Retry
            </Button>
          </Card>
        </div>
      </div>
    );
  }

  if (!workout) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="max-w-6xl mx-auto p-6">
          <Card className="p-8 text-center">
            <h2 className="text-2xl font-bold mb-2">No Active Workout</h2>
            <p className="text-muted-foreground mb-6">
              You don't have an active workout program yet. Let's create one!
            </p>
            <div className="flex gap-4 justify-center">
              <Button onClick={() => navigate("/setup")}>Create Workout Program</Button>
              <Button variant="outline" onClick={() => navigate("/programs")}>View All Programs</Button>
            </div>
          </Card>
        </div>
      </div>
    );
  }

  const currentBlock = Math.ceil(workout.currentWeek / 7);
  const weekInBlock = ((workout.currentWeek - 1) % 7) + 1;

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="max-w-6xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold mb-2">{workout.name}</h1>
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <span>
              Week {workout.currentWeek} of {workout.totalWeeks}
            </span>
            <span>-</span>
            <span>
              Block {currentBlock}, Week {weekInBlock}
            </span>
            <span>-</span>
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

        {/* Exercises Summary with Full Details */}
        <Card className="mt-6 p-6">
          <h2 className="text-xl font-bold mb-4">Your Exercises</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {workout.exercises.map((exercise) => {
              const isLinear = exercise.progression.type === "Linear";
              const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
              const repsPerSetProg = !isLinear ? (exercise.progression as RepsPerSetProgressionDto) : null;

              return (
                <div key={exercise.id} className="p-4 border rounded-lg bg-card hover:border-primary/30 transition-colors">
                  <div className="flex justify-between items-start mb-2">
                    <div className="font-semibold text-foreground">{exercise.name}</div>
                    <span className="text-xs px-2 py-0.5 rounded-full bg-primary/10 text-primary">
                      Day {exercise.assignedDay}
                    </span>
                  </div>

                  {linearProg && (
                    <div className="space-y-1 text-sm">
                      <div className="flex justify-between text-muted-foreground">
                        <span>Training Max:</span>
                        <span className="font-medium text-foreground">
                          {linearProg.trainingMax.value} {linearProg.trainingMax.unit === WeightUnit.Kilograms ? "kg" : "lbs"}
                        </span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Sets:</span>
                        <span className="font-medium text-foreground">{linearProg.baseSetsPerExercise}</span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>AMRAP:</span>
                        <span className={`font-medium ${linearProg.useAmrap ? "text-primary" : "text-muted-foreground"}`}>
                          {linearProg.useAmrap ? "Yes (last set)" : "No"}
                        </span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Progression:</span>
                        <span className="font-medium text-foreground">Linear (A2S)</span>
                      </div>
                    </div>
                  )}

                  {repsPerSetProg && (
                    <div className="space-y-1 text-sm">
                      <div className="flex justify-between text-muted-foreground">
                        <span>Weight:</span>
                        <span className="font-medium text-foreground">
                          {repsPerSetProg.currentWeight} {repsPerSetProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
                        </span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Sets:</span>
                        <span className="font-medium text-foreground">
                          {repsPerSetProg.currentSetCount} / {repsPerSetProg.targetSets}
                        </span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Rep Range:</span>
                        <span className="font-medium text-foreground">
                          {repsPerSetProg.repRange.minimum}-{repsPerSetProg.repRange.target}-{repsPerSetProg.repRange.maximum}
                        </span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Target Reps:</span>
                        <span className="font-medium text-foreground">{repsPerSetProg.repRange.target}</span>
                      </div>
                      <div className="flex justify-between text-muted-foreground">
                        <span>Progression:</span>
                        <span className="font-medium text-foreground">Reps Per Set</span>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </Card>
      </div>
    </div>
  );
}
