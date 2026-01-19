import type { SelectedExercise, DayNumber, ProgramVariant } from "@/types/workout";
import { SelectedExerciseCard } from "./SelectedExerciseCard";

interface SimpleDayColumnsViewProps {
  exercises: SelectedExercise[];
  programVariant: ProgramVariant;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}

/**
 * Day column without drag and drop
 */
function DayColumn({
  day,
  exercises,
  onEdit,
  onRemove,
}: {
  day: DayNumber;
  exercises: SelectedExercise[];
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
}) {
  const sortedExercises = [...exercises].sort((a, b) => a.orderInDay - b.orderInDay);

  return (
    <div className="flex flex-col h-full">
      {/* Day header */}
      <div className="mb-3 pb-3 border-b border-border">
        <div className="flex items-center gap-2">
          <span className="inline-flex items-center justify-center w-7 h-7 rounded-full bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 text-sm font-bold">
            {day}
          </span>
          <h3 className="text-base font-semibold text-foreground">Day {day}</h3>
        </div>
        <p className="text-xs text-muted-foreground mt-1">
          {exercises.length} {exercises.length === 1 ? "exercise" : "exercises"}
        </p>
      </div>

      {/* Exercises list */}
      <div className="flex-1 space-y-2">
        {sortedExercises.length === 0 ? (
          <div className="h-full flex items-center justify-center border-2 border-dashed border-border rounded-lg p-6">
            <p className="text-sm text-muted-foreground text-center">
              No exercises yet
            </p>
          </div>
        ) : (
          sortedExercises.map((exercise) => (
            <SelectedExerciseCard
              key={exercise.id}
              exercise={exercise}
              onEdit={onEdit}
              onRemove={onRemove}
              showOrder={true}
              showDragHandle={false}
            />
          ))
        )}
      </div>
    </div>
  );
}

/**
 * Simple day columns view without drag-and-drop
 */
export function SimpleDayColumnsView({
  exercises,
  programVariant,
  onEdit,
  onRemove,
}: SimpleDayColumnsViewProps) {
  // Group exercises by day
  const exercisesByDay: Record<DayNumber, SelectedExercise[]> = {
    1: [],
    2: [],
    3: [],
    4: [],
    5: [],
    6: [],
  };

  exercises.forEach((exercise) => {
    exercisesByDay[exercise.assignedDay].push(exercise);
  });

  // Get available days based on program variant
  const availableDays: DayNumber[] = [1, 2, 3, 4];
  if (programVariant >= 5) availableDays.push(5);
  if (programVariant >= 6) availableDays.push(6);

  if (exercises.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        <svg
          className="w-12 h-12 mx-auto mb-3 opacity-50"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
        </svg>
        <p className="font-medium">No exercises selected yet</p>
        <p className="text-sm mt-1">Add exercises from the library to get started</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
      {availableDays.map((day) => (
        <div key={day} className="bg-muted/30 rounded-lg p-4 border border-border">
          <DayColumn
            day={day}
            exercises={exercisesByDay[day]}
            onEdit={onEdit}
            onRemove={onRemove}
          />
        </div>
      ))}
    </div>
  );
}
