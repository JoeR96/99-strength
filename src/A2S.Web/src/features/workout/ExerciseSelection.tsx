import { useState } from "react";
import { useExerciseLibrary } from "@/hooks/useWorkouts";
import type { ExerciseDefinition } from "@/types/workout";

interface ExerciseSelectionProps {
  selectedExercises: ExerciseDefinition[];
  onUpdate: (exercises: ExerciseDefinition[]) => void;
}

export function ExerciseSelection({ selectedExercises, onUpdate }: ExerciseSelectionProps) {
  const { data: library, isLoading } = useExerciseLibrary();
  const [activeCategory, setActiveCategory] = useState<"auxiliary" | "accessory">("auxiliary");

  const toggleExercise = (exercise: ExerciseDefinition) => {
    const isSelected = selectedExercises.some((e) => e.name === exercise.name);

    if (isSelected) {
      onUpdate(selectedExercises.filter((e) => e.name !== exercise.name));
    } else {
      onUpdate([...selectedExercises, exercise]);
    }
  };

  const isSelected = (exercise: ExerciseDefinition) => {
    return selectedExercises.some((e) => e.name === exercise.name);
  };

  if (isLoading) {
    return <div className="text-center py-8">Loading exercises...</div>;
  }

  if (!library) {
    return <div className="text-center py-8 text-muted-foreground">No exercises available</div>;
  }

  const auxiliaryExercises = library.auxiliaryLifts || [];
  const accessoryExercises = library.accessories || [];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold mb-2">Select Additional Exercises</h2>
        <p className="text-muted-foreground">
          The 4 main lifts (Squat, Bench, Deadlift, OHP) are included by default.
          Add auxiliary and accessory exercises to customize your program.
        </p>
      </div>

      {/* Main lifts (always included) */}
      <div>
        <h3 className="font-semibold mb-3">Main Lifts (Required)</h3>
        <div className="grid grid-cols-2 gap-2">
          {library.mainLifts.map((exercise) => (
            <div
              key={exercise.name}
              className="p-3 border rounded-md bg-primary/10 border-primary"
            >
              <div className="font-medium">{exercise.name}</div>
              <div className="text-xs text-muted-foreground mt-1">Always included</div>
            </div>
          ))}
        </div>
      </div>

      {/* Category tabs */}
      <div>
        <div className="flex gap-2 mb-4">
          <button
            type="button"
            onClick={() => setActiveCategory("auxiliary")}
            className={`px-4 py-2 rounded-md ${
              activeCategory === "auxiliary"
                ? "bg-primary text-primary-foreground"
                : "bg-muted"
            }`}
          >
            Auxiliary Lifts ({auxiliaryExercises.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveCategory("accessory")}
            className={`px-4 py-2 rounded-md ${
              activeCategory === "accessory"
                ? "bg-primary text-primary-foreground"
                : "bg-muted"
            }`}
          >
            Accessories ({accessoryExercises.length})
          </button>
        </div>

        {/* Exercise list */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          {activeCategory === "auxiliary" &&
            auxiliaryExercises.map((exercise) => (
              <ExerciseCard
                key={exercise.name}
                exercise={exercise}
                isSelected={isSelected(exercise)}
                onToggle={() => toggleExercise(exercise)}
              />
            ))}

          {activeCategory === "accessory" &&
            accessoryExercises.map((exercise) => (
              <ExerciseCard
                key={exercise.name}
                exercise={exercise}
                isSelected={isSelected(exercise)}
                onToggle={() => toggleExercise(exercise)}
              />
            ))}
        </div>
      </div>

      {/* Selection summary */}
      {selectedExercises.length > 0 && (
        <div className="mt-4 p-4 bg-muted rounded-md">
          <h4 className="font-semibold text-sm mb-2">
            Selected: {selectedExercises.length} exercises
          </h4>
          <div className="flex flex-wrap gap-2">
            {selectedExercises.map((exercise) => (
              <span
                key={exercise.name}
                className="px-2 py-1 text-xs bg-primary text-primary-foreground rounded-md"
              >
                {exercise.name}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

interface ExerciseCardProps {
  exercise: ExerciseDefinition;
  isSelected: boolean;
  onToggle: () => void;
}

function ExerciseCard({ exercise, isSelected, onToggle }: ExerciseCardProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      className={`text-left p-3 border rounded-md transition-colors ${
        isSelected
          ? "border-primary bg-primary/5"
          : "border-border hover:border-primary/50"
      }`}
    >
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <div className="font-medium">{exercise.name}</div>
          <div className="text-xs text-muted-foreground mt-1">
            {exercise.description}
          </div>
          {exercise.defaultRepRange && (
            <div className="text-xs text-muted-foreground mt-1">
              Rep range: {exercise.defaultRepRange.minimum}-
              {exercise.defaultRepRange.target}-{exercise.defaultRepRange.maximum}
            </div>
          )}
        </div>
        <div className="ml-2">
          <div
            className={`w-5 h-5 rounded border-2 flex items-center justify-center ${
              isSelected ? "border-primary bg-primary" : "border-muted-foreground"
            }`}
          >
            {isSelected && (
              <svg
                className="w-3 h-3 text-primary-foreground"
                fill="none"
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path d="M5 13l4 4L19 7"></path>
              </svg>
            )}
          </div>
        </div>
      </div>
    </button>
  );
}
