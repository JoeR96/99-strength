import { useEffect, useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
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
  onEdit: (exercise: ExerciseDto) => void;
  isTemporarilySubstituted: boolean;
  originalName?: string;
  /** Collapse fully-completed cards to a one-line summary (Hevy prefill review). */
  defaultCollapsed?: boolean;
}

export function ExerciseCard({
  entry,
  exerciseIndex,
  onSetChange,
  onSetComplete,
  onSubstitute,
  onEdit,
  isTemporarilySubstituted,
  originalName,
  defaultCollapsed = false,
}: ExerciseCardProps) {
  const allCompleted = entry.sets.every((s) => s.completed);
  const isRepsPerSet = entry.exercise.progression.type === "RepsPerSet";
  const repsPerSetProg = isRepsPerSet ? (entry.exercise.progression as RepsPerSetProgressionDto) : null;

  const [collapsed, setCollapsed] = useState(defaultCollapsed && allCompleted);
  // Prefill arrives after mount, so collapse when it lands (but never re-collapse
  // after the user expands a card to edit).
  useEffect(() => {
    if (defaultCollapsed) setCollapsed(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [defaultCollapsed]);

  if (collapsed && allCompleted) {
    const uniformWeight = entry.sets.every((s) => s.weight === entry.sets[0].weight);
    const summary = uniformWeight
      ? `${entry.sets[0].weight} ${entry.weightUnit} × ${entry.sets.map((s) => s.reps).join(" / ")}`
      : entry.sets.map((s) => `${s.reps}×${s.weight}${entry.weightUnit}`).join(", ");
    return (
      <Card
        className="p-3 border-success bg-success/10 cursor-pointer"
        data-testid={`exercise-card-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}
        onClick={() => setCollapsed(false)}
        role="button"
        aria-expanded={false}
        aria-label={`Expand ${entry.exercise.name}`}
      >
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2 min-w-0">
            <svg className="w-5 h-5 text-success shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                clipRule="evenodd"
              />
            </svg>
            <span className="font-semibold truncate">{entry.exercise.name}</span>
            {repsPerSetProg?.isUnilateral && (
              <Badge variant="info" className="shrink-0">Per Side</Badge>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <span className="text-sm text-muted-foreground" data-testid={`set-summary-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}>
              {summary}
            </span>
            <svg className="w-4 h-4 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>
      </Card>
    );
  }

  return (
    <Card
      className={`p-4 ${allCompleted ? "border-success bg-success/10" : ""}`}
      data-testid={`exercise-card-${entry.exercise.name.replace(/\s+/g, "-").toLowerCase()}`}
    >
      <div className="flex items-center justify-between mb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-lg">{entry.exercise.name}</h3>
            {isTemporarilySubstituted && (
              <Badge variant="warning">Temp Sub</Badge>
            )}
            {repsPerSetProg?.isUnilateral && (
              <Badge variant="info">Per Side</Badge>
            )}
            {repsPerSetProg?.pendingWeightConfirmation && (
              <Badge variant="warning">New weight — match your stack</Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {entry.exercise.progression.type} Progression
            {entry.isAmrapExercise && " - AMRAP on last set"}
            {isTemporarilySubstituted && originalName && (
              <span className="ml-2 text-yellow-600">
                (replacing {originalName})
              </span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => onEdit(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            aria-label="Edit exercise configuration"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </Button>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => onSubstitute(entry.exercise)}
            className="text-muted-foreground hover:text-foreground"
            aria-label="Substitute exercise"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
          </Button>
          {allCompleted && (
            <div className="text-success">
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
                <div className="mb-2 p-3 rounded-lg bg-gradient-to-r from-orange-100 to-amber-100 border border-orange-200">
                  <div className="flex items-center gap-2 text-orange-700 font-semibold">
                    <span className="text-lg">🔥</span>
                    <span>FINAL SET - AMRAP</span>
                  </div>
                  <p className="text-sm text-orange-600 mt-1">
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
                } ${set.isAmrap && !set.completed ? "p-2 rounded-lg bg-primary/10 border border-primary/20" : ""}`}
                data-testid={`set-row-${set.setNumber}`}
              >
                <div className="col-span-1 font-medium">
                  {set.setNumber}
                  {set.isAmrap && (
                    <span className="text-xs text-orange-500 ml-1">🔥</span>
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
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 focus:border-orange-500 focus:ring-orange-500" : ""}`}
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
                    className={`h-8 ${set.isAmrap && !set.completed ? "border-orange-300 focus:border-orange-500 focus:ring-orange-500" : ""}`}
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
