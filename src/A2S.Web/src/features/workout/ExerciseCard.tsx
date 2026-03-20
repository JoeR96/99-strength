import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ExerciseEntry, ExerciseDto, RepsPerSetProgressionDto } from "./workoutSessionTypes";

interface ExerciseCardProps {
  entry: ExerciseEntry;
  exerciseIndex: number;
  onSetChange: (
    exerciseIndex: number,
    setIndex: number,
    field: "weight" | "reps",
    value: number
  ) => void;
  onSetComplete: (exerciseIndex: number, setIndex: number) => void;
  onSubstitute: (exercise: ExerciseDto) => void;
  onToggleUnilateral: (exercise: ExerciseDto) => void;
  onEdit: (exercise: ExerciseDto) => void;
  isTemporarilySubstituted: boolean;
  originalName?: string;
}

export function ExerciseCard({
  entry,
  exerciseIndex,
  onSetChange,
  onSetComplete,
  onSubstitute,
  onToggleUnilateral,
  onEdit,
  isTemporarilySubstituted,
  originalName,
}: ExerciseCardProps) {
  const allCompleted = entry.sets.every((s) => s.completed);
  const isRepsPerSet = entry.exercise.progression.type === "RepsPerSet";
  const repsPerSetProg = isRepsPerSet ? (entry.exercise.progression as RepsPerSetProgressionDto) : null;

  return (
    <Card
      className={`p-4 ${allCompleted ? "border-green-500 bg-green-50 dark:bg-green-950/20" : ""}`}
      data-testid={`exercise-card-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}
    >
      <div className="flex items-center justify-between mb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-lg">{entry.exercise.name}</h3>
            {isTemporarilySubstituted && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400">
                Temp Sub
              </span>
            )}
            {repsPerSetProg?.isUnilateral && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400">
                Per Side
              </span>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {entry.exercise.progression.type} Progression
            {entry.isAmrapExercise && " - AMRAP on last set"}
            {isTemporarilySubstituted && originalName && (
              <span className="ml-2 text-yellow-600 dark:text-yellow-400">
                (replacing {originalName})
              </span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onEdit(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            aria-label="Edit exercise configuration"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </Button>
          {isRepsPerSet && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onToggleUnilateral(entry.exercise)}
              className={`text-xs px-2 ${repsPerSetProg?.isUnilateral ? "text-blue-600 dark:text-blue-400" : "text-muted-foreground hover:text-foreground"}`}
              aria-label={repsPerSetProg?.isUnilateral ? "Switch to bilateral (both sides together)" : "Switch to unilateral (one side at a time)"}
            >
              {repsPerSetProg?.isUnilateral ? "1-Arm" : "2-Arm"}
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onSubstitute(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            aria-label="Substitute exercise"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
          </Button>
          {allCompleted && (
            <div className="text-green-500">
              <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
          )}
        </div>
      </div>

      {/* Sets */}
      <div className="space-y-3">
        <div className="grid grid-cols-12 gap-2 text-xs font-medium text-muted-foreground">
          <div className="col-span-1">Set</div>
          <div className="col-span-4">Weight ({entry.weightUnit})</div>
          <div className="col-span-4">Reps</div>
          <div className="col-span-3">Done</div>
        </div>

        {entry.sets.map((set, setIndex) => {
          const getAmrapProgressionHint = () => {
            if (!set.isAmrap || entry.exercise.progression.type !== "Linear") return null;
            const progressionThreshold = entry.targetReps + 3;
            return progressionThreshold;
          };
          const amrapHint = getAmrapProgressionHint();

          return (
            <div key={set.setNumber}>
              {set.isAmrap && !set.completed && (
                <div className="mb-2 p-3 rounded-lg bg-gradient-to-r from-orange-100 to-amber-100 dark:from-orange-950/40 dark:to-amber-950/40 border border-orange-200 dark:border-orange-800">
                  <div className="flex items-center gap-2 text-orange-700 dark:text-orange-300 font-semibold">
                    <span className="text-lg">🔥</span>
                    <span>FINAL SET - AMRAP</span>
                  </div>
                  <p className="text-sm text-orange-600 dark:text-orange-400 mt-1">
                    As Many Reps As Possible!
                    {amrapHint && (
                      <span className="ml-2 font-medium">
                        Try for {amrapHint}+ for TM ↑
                      </span>
                    )}
                  </p>
                </div>
              )}
              <div
                className={`grid grid-cols-12 gap-2 items-center ${
                  set.completed ? "opacity-60" : ""
                } ${set.isAmrap && !set.completed ? "p-2 rounded-lg bg-orange-50/50 dark:bg-orange-950/20 border border-orange-100 dark:border-orange-900/50" : ""}`}
                data-testid={`set-row-${set.setNumber}`}
              >
                <div className="col-span-1 font-medium">
                  {set.setNumber}
                  {set.isAmrap && (
                    <span className="text-xs text-orange-500 dark:text-orange-400 ml-1">🔥</span>
                  )}
                </div>
                <div className="col-span-4">
                  <Input
                    type="number"
                    value={set.weight}
                    onChange={(e) =>
                      onSetChange(
                        exerciseIndex,
                        setIndex,
                        "weight",
                        parseFloat(e.target.value) || 0
                      )
                    }
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 dark:border-orange-700 focus:border-orange-500 focus:ring-orange-500" : ""}`}
                    data-testid={`weight-input-${set.setNumber}`}
                    disabled={set.completed}
                    aria-label={`Weight for set ${set.setNumber}`}
                  />
                </div>
                <div className="col-span-4">
                  <Input
                    type="number"
                    value={set.reps}
                    onChange={(e) =>
                      onSetChange(
                        exerciseIndex,
                        setIndex,
                        "reps",
                        parseInt(e.target.value) || 0
                      )
                    }
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 dark:border-orange-700 focus:border-orange-500 focus:ring-orange-500" : ""}`}
                    data-testid={`reps-input-${set.setNumber}`}
                    disabled={set.completed}
                    aria-label={`Reps for set ${set.setNumber}`}
                  />
                </div>
                <div className="col-span-3">
                  <Button
                    variant={set.completed ? "default" : set.isAmrap ? "default" : "outline"}
                    size="sm"
                    className={`w-full h-8 ${set.isAmrap && !set.completed ? "bg-orange-500 hover:bg-orange-600 text-white" : ""}`}
                    onClick={() => onSetComplete(exerciseIndex, setIndex)}
                    data-testid={`complete-set-${set.setNumber}`}
                  >
                    {set.completed ? "Done" : set.isAmrap ? "Log AMRAP" : "Log"}
                  </Button>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </Card>
  );
}
