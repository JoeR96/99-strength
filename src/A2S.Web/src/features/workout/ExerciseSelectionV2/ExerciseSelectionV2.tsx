import { useState, useEffect } from "react";
import { useExerciseSelection } from "../../../hooks/useExerciseSelection";
import { useExerciseLibrary } from "../../../hooks/useWorkouts";
import type {
  ExerciseTemplate,
  SelectedExercise,
  ProgramVariant,
} from "../../../types/workout";
import { ExerciseLibraryBrowser } from "./ExerciseLibraryBrowser";
import { SimpleDayColumnsView } from "./SimpleDayColumnsView";
import { ExerciseConfigDialog } from "./ExerciseConfigDialog";
// import { Alert, AlertDescription } from "../../../components/ui/alert";

interface ExerciseSelectionV2Props {
  selectedExercises: SelectedExercise[];
  onUpdate: (exercises: SelectedExercise[]) => void;
  programVariant: ProgramVariant;
}

/**
 * Main container for the new exercise selection experience.
 * Provides a two-panel layout with library browsing on the left
 * and selected exercises with configuration on the right.
 */
export function ExerciseSelectionV2({
  selectedExercises: initialExercises = [],
  onUpdate,
  programVariant = 4,
}: ExerciseSelectionV2Props) {
  const {
    selectedExercises,
    addExercise,
    removeExercise,
    updateExercise,
    setExercises,
  } = useExerciseSelection(programVariant);

  const { data: library, isLoading, error } = useExerciseLibrary();

  // Dialog state for configuring exercises
  const [editingExerciseId, setEditingExerciseId] = useState<string | null>(null);

  // Initialize from props
  useState(() => {
    if (initialExercises.length > 0) {
      setExercises(initialExercises);
    }
  });

  // Sync state changes back to parent using useEffect to handle async state updates
  useEffect(() => {
    // Only sync if we have exercises (avoid syncing initial empty state)
    if (selectedExercises.length > 0 || initialExercises.length === 0) {
      onUpdate(selectedExercises);
    }
  }, [selectedExercises, initialExercises.length, onUpdate]);

  // Handle adding an exercise from the library
  const handleAddExercise = (template: ExerciseTemplate) => {
    const exerciseId = addExercise(template);
    // Open config dialog immediately for the new exercise
    setEditingExerciseId(exerciseId);
    // Note: Parent sync happens via useEffect when selectedExercises changes
  };

  // Handle removing an exercise
  const handleRemoveExercise = (id: string) => {
    removeExercise(id);
    // Note: Parent sync happens via useEffect when selectedExercises changes
  };

  // Handle opening the config dialog
  const handleEditExercise = (exercise: SelectedExercise) => {
    setEditingExerciseId(exercise.id);
  };

  // Handle saving exercise configuration
  const handleSaveConfig = (
    id: string,
    updates: Partial<Omit<SelectedExercise, "id" | "template">>
  ) => {
    updateExercise(id, updates);
    setEditingExerciseId(null);
    // Note: Parent sync happens via useEffect when selectedExercises changes
  };

  // Find the exercise being edited
  const editingExercise = selectedExercises.find(
    (ex) => ex.id === editingExerciseId
  );

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-8">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4"></div>
          <p className="text-muted-foreground">Loading exercise library...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 bg-red-50 text-red-900 rounded-md">
        Failed to load exercise library. Please try again.
      </div>
    );
  }

  if (!library) {
    return (
      <div className="p-4 bg-yellow-50 text-yellow-900 rounded-md">
        No exercise library available.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold text-foreground mb-2">
          Select Exercises
        </h2>
        <p className="text-muted-foreground">
          Browse the exercise library and add exercises to your program. Configure
          each exercise's category, progression type, and training day.
        </p>
      </div>

      {/* Exercise Library */}
      <div className="space-y-4">
        <h3 className="text-lg font-semibold text-foreground">
          Exercise Library
        </h3>
        <ExerciseLibraryBrowser
          templates={library.templates}
          onAddExercise={handleAddExercise}
        />
      </div>

      {/* Day Columns */}
      <div className="space-y-4">
        <h3 className="text-lg font-semibold text-foreground">
          Your Program ({selectedExercises.length} {selectedExercises.length === 1 ? 'exercise' : 'exercises'})
        </h3>
        <SimpleDayColumnsView
          exercises={selectedExercises}
          programVariant={programVariant}
          onEdit={handleEditExercise}
          onRemove={handleRemoveExercise}
        />
      </div>

      {/* Configuration Dialog */}
      {editingExercise && (
        <ExerciseConfigDialog
          exercise={editingExercise}
          programVariant={programVariant}
          isOpen={editingExerciseId !== null}
          onClose={() => setEditingExerciseId(null)}
          onSave={handleSaveConfig}
        />
      )}
    </div>
  );
}
