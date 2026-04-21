import { useState } from "react";
import { useCurrentWorkout, useUpdateExercises, useSubstituteExercise } from "@/hooks/useWorkouts";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { WeekOverview } from "./WeekOverview";
import { NextWeekPreview } from "./NextWeekPreview";
import { BlockSequenceEditor } from "./BlockSequenceEditor";
import { ExerciseProgressionModal } from "./ExerciseProgressionModal";
import type { ExerciseConfigUpdate } from "./EditExerciseConfigModal";
import { useNavigate } from "react-router-dom";
import { Navbar } from "@/components/layout/Navbar";
import { WeightUnit, type ExerciseDto, type LinearProgressionDto, type RepsPerSetProgressionDto, type MinimalSetsProgressionDto, type ProgressionConfigRequest } from "@/types/workout";
import { getBlockType } from "@/utils/weekParameters";
import toast from "react-hot-toast";

export function WorkoutDashboard() {
  const { data: workout, isLoading, error, refetch } = useCurrentWorkout();
  const navigate = useNavigate();
  const [showBlockEditor, setShowBlockEditor] = useState(false);
  const [selectedExercise, setSelectedExercise] = useState<ExerciseDto | null>(null);
  const updateExercises = useUpdateExercises();
  const substituteExercise = useSubstituteExercise();

  const handleSaveExerciseConfig = async (exerciseId: string, config: ExerciseConfigUpdate) => {
    if (!workout) return;
    await updateExercises.mutateAsync({
      workoutId: workout.id,
      request: {
        updates: [{
          exerciseId,
          trainingMaxValue: config.trainingMaxValue,
          trainingMaxUnit: config.trainingMaxUnit,
          weightValue: config.weightValue,
          weightUnit: config.weightUnit,
        }],
      },
    });
    toast.success("Exercise updated");
    refetch();
  };

  const handleChangeProgression = async (exerciseId: string, config: ProgressionConfigRequest) => {
    if (!workout) return;
    const exercise = workout.exercises.find(e => e.id === exerciseId);
    if (!exercise) return;

    toast.loading("Changing progression...", { id: "change-progression" });
    await substituteExercise.mutateAsync({
      workoutId: workout.id,
      request: {
        exerciseId,
        newExerciseName: exercise.name,
        reason: `Changed progression from ${exercise.progression.type} to ${config.type}`,
        newProgressionConfig: config,
      },
    });
    toast.success("Progression type changed", { id: "change-progression" });
    refetch();
  };

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

  const blockSequence = workout.blockSequence ?? [1, 2, 3];
  const blockIndex = Math.floor((workout.currentWeek - 1) / 7);
  const currentBlockType = getBlockType(workout.currentWeek, blockSequence);
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
              Block {blockIndex + 1}/{blockSequence.length} (Type {currentBlockType}), Week {weekInBlock}
            </span>
            <span>-</span>
            <span>{workout.variant}-Day Program</span>
            <Button
              variant="ghost"
              size="sm"
              className="text-xs px-2 py-1 h-auto"
              onClick={() => setShowBlockEditor(true)}
            >
              Manage Blocks
            </Button>
          </div>
        </div>

        {/* Block Sequence Visual */}
        <Card className="p-4 mb-6">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-medium text-muted-foreground mr-2">Blocks:</span>
            {blockSequence.map((blockType, idx) => {
              const isCurrentBlock = idx === blockIndex;
              return (
                <div
                  key={idx}
                  className={`px-3 py-1 rounded-full text-xs font-medium transition-all ${
                    isCurrentBlock
                      ? "bg-primary text-primary-foreground ring-2 ring-primary/30"
                      : idx < blockIndex
                      ? "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300"
                      : "bg-muted text-muted-foreground"
                  }`}
                >
                  B{blockType}
                </div>
              );
            })}
            <span className="text-xs text-muted-foreground ml-2">
              = {blockSequence.length * 7} weeks
            </span>
          </div>
        </Card>

        {/* Progress bar */}
        <Card className="p-6 mb-6">
          {(() => {
            const completedDays = (workout.currentWeek - 1) * workout.daysPerWeek + workout.completedDaysInCurrentWeek.length;
            const totalDays = workout.totalWeeks * workout.daysPerWeek;
            const pct = Math.round((completedDays / totalDays) * 100);
            return (
              <>
                <div className="flex items-center justify-between mb-2">
                  <span className="font-semibold">Program Progress</span>
                  <span className="text-sm text-muted-foreground">
                    {completedDays}/{totalDays} days ({pct}%)
                  </span>
                </div>
                <div className="h-2 bg-muted rounded-full overflow-hidden">
                  <div
                    className="h-full bg-primary transition-all duration-300"
                    style={{ width: `${pct}%` }}
                  />
                </div>
              </>
            );
          })()}
        </Card>

        {/* Week Overview */}
        <WeekOverview workout={workout} onWorkoutUpdated={refetch} />

        {/* Next Week Preview */}
        <NextWeekPreview workout={workout} onWorkoutUpdated={refetch} />

        {/* Exercises Summary grouped by Day */}
        <Card className="mt-6 p-6">
          <h2 className="text-xl font-bold mb-4">Your Exercises</h2>
          {(() => {
            const exercisesByDay = workout.exercises.reduce((acc, ex) => {
              const day = ex.assignedDay;
              if (!acc[day]) acc[day] = [];
              acc[day].push(ex);
              return acc;
            }, {} as Record<number, ExerciseDto[]>);

            const sortedDays = Object.keys(exercisesByDay).map(Number).sort((a, b) => a - b);

            return sortedDays.map((day) => (
              <div key={day} className="mb-6 last:mb-0">
                <h3 className="text-lg font-semibold mb-3 text-muted-foreground">Day {day}</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {exercisesByDay[day]
                    .sort((a, b) => a.orderInDay - b.orderInDay)
                    .map((exercise) => {
                      const isLinear = exercise.progression.type === "Linear";
                      const isRepsPerSet = exercise.progression.type === "RepsPerSet";
                      const isMinimalSets = exercise.progression.type === "MinimalSets";
                      const linearProg = isLinear ? (exercise.progression as LinearProgressionDto) : null;
                      const repsPerSetProg = isRepsPerSet ? (exercise.progression as RepsPerSetProgressionDto) : null;
                      const minimalSetsProg = isMinimalSets ? (exercise.progression as MinimalSetsProgressionDto) : null;

                      return (
                        <div
                          key={exercise.id}
                          className="p-4 border rounded-lg bg-card hover:border-primary/30 transition-colors cursor-pointer"
                          onClick={() => setSelectedExercise(exercise)}
                        >
                          <div className="font-semibold text-foreground mb-2">{exercise.name}</div>

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
                                <span className="font-medium text-foreground">Linear (Hypertrophy)</span>
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
                                  {repsPerSetProg.repRange?.minimum ?? 0}-{repsPerSetProg.repRange?.maximum ?? 0}
                                </span>
                              </div>
                              <div className="flex justify-between text-muted-foreground">
                                <span>Progression:</span>
                                <span className="font-medium text-foreground">Reps Per Set</span>
                              </div>
                            </div>
                          )}

                          {minimalSetsProg && (
                            <div className="space-y-1 text-sm">
                              <div className="flex justify-between text-muted-foreground">
                                <span>Weight:</span>
                                <span className="font-medium text-foreground">
                                  {minimalSetsProg.currentWeight} {minimalSetsProg.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
                                </span>
                              </div>
                              <div className="flex justify-between text-muted-foreground">
                                <span>Sets:</span>
                                <span className="font-medium text-foreground">
                                  {minimalSetsProg.currentSetCount} ({minimalSetsProg.minimumSets}-{minimalSetsProg.maximumSets})
                                </span>
                              </div>
                              <div className="flex justify-between text-muted-foreground">
                                <span>Target Total Reps:</span>
                                <span className="font-medium text-foreground">{minimalSetsProg.targetTotalReps}</span>
                              </div>
                              <div className="flex justify-between text-muted-foreground">
                                <span>Progression:</span>
                                <span className="font-medium text-foreground">Minimal Sets</span>
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}
                </div>
              </div>
            ));
          })()}
        </Card>

        {/* Exercise Progression Modal */}
        {selectedExercise && (
          <ExerciseProgressionModal
            exercise={selectedExercise}
            workoutId={workout.id}
            blockSequence={blockSequence}
            isOpen={!!selectedExercise}
            onClose={() => setSelectedExercise(null)}
            onSave={handleSaveExerciseConfig}
            onChangeProgression={handleChangeProgression}
            onWorkoutUpdated={() => {
              refetch();
              setSelectedExercise(null);
            }}
          />
        )}

        {/* Block Sequence Editor Modal */}
        <BlockSequenceEditor
          workout={workout}
          isOpen={showBlockEditor}
          onClose={() => setShowBlockEditor(false)}
          onUpdated={refetch}
        />
      </div>
    </div>
  );
}
