import type { SelectedExercise, ExerciseCategory, EquipmentType, A2SProgressionType } from "@/types/workout";

interface SelectedExerciseCardProps {
  exercise: SelectedExercise;
  onEdit: (exercise: SelectedExercise) => void;
  onRemove: (id: string) => void;
  isDragging?: boolean;
  showOrder?: boolean;
  showDragHandle?: boolean;
}

/**
 * Get category label from enum value
 */
function getCategoryLabel(category: ExerciseCategory): string {
  const labels: Record<ExerciseCategory, string> = {
    1: "Main Lift",
    2: "Auxiliary",
    3: "Accessory",
  };
  return labels[category] || "Unknown";
}

/**
 * Get category color classes
 */
function getCategoryColor(category: ExerciseCategory): string {
  const colors: Record<ExerciseCategory, string> = {
    1: "bg-primary/10 text-primary border-primary/20",
    2: "bg-blue-500/10 text-blue-600 border-blue-500/20 dark:text-blue-400",
    3: "bg-purple-500/10 text-purple-600 border-purple-500/20 dark:text-purple-400",
  };
  return colors[category] || "bg-muted text-muted-foreground";
}

/**
 * Get progression type label
 */
function getProgressionLabel(type: A2SProgressionType): string {
  return type === "Hypertrophy" ? "A2S Hypertrophy" : "A2S Reps Per Set";
}

/**
 * Get equipment label from enum value
 */
function getEquipmentLabel(equipment: EquipmentType): string {
  const labels: Record<EquipmentType, string> = {
    0: "Barbell",
    1: "Dumbbell",
    2: "Cable",
    3: "Machine",
    4: "Bodyweight",
    5: "Smith Machine",
  };
  return labels[equipment] || "Unknown";
}

/**
 * Display a selected and configured exercise with edit and remove actions
 */
export function SelectedExerciseCard({
  exercise,
  onEdit,
  onRemove,
  isDragging = false,
  showOrder = false,
  showDragHandle = true,
}: SelectedExerciseCardProps) {
  return (
    <div
      className={`p-2 border rounded-md bg-card transition-all ${
        isDragging ? "opacity-50 shadow-lg" : "border-border"
      }`}
    >
      {/* Top row: Order, Name, Actions */}
      <div className="flex items-center gap-2 mb-1.5">
        {/* Order number */}
        {showOrder && (
          <div className="flex items-center justify-center w-5 h-5 rounded-full bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 text-xs font-bold shrink-0">
            {exercise.orderInDay}
          </div>
        )}

        {/* Drag handle indicator */}
        {showDragHandle && (
          <div className="text-muted-foreground cursor-grab active:cursor-grabbing shrink-0">
            <svg
              className="w-3.5 h-3.5"
              fill="currentColor"
              viewBox="0 0 16 16"
              aria-label="Drag to reorder"
            >
              <circle cx="5" cy="3" r="1.5" />
              <circle cx="11" cy="3" r="1.5" />
              <circle cx="5" cy="8" r="1.5" />
              <circle cx="11" cy="8" r="1.5" />
              <circle cx="5" cy="13" r="1.5" />
              <circle cx="11" cy="13" r="1.5" />
            </svg>
          </div>
        )}

        {/* Exercise name */}
        <h4 className="font-medium text-sm flex-1 min-w-0 truncate">{exercise.template.name}</h4>

        {/* Action buttons */}
        <div className="flex items-center gap-1 shrink-0">
          <button
            type="button"
            onClick={() => onEdit(exercise)}
            className="p-1 rounded-md hover:bg-muted transition-colors"
            aria-label={`Edit ${exercise.template.name}`}
          >
            <svg
              className="w-3.5 h-3.5 text-muted-foreground"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>

          <button
            type="button"
            onClick={() => onRemove(exercise.id)}
            className="p-1 rounded-md hover:bg-destructive/10 transition-colors"
            aria-label={`Remove ${exercise.template.name}`}
          >
            <svg
              className="w-3.5 h-3.5 text-destructive"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="2"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </div>

      {/* Single compact row with all key info */}
      <div className="text-xs text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1">
        {/* Equipment */}
        <span className="font-medium">{getEquipmentLabel(exercise.template.equipment)}</span>

        <span className="text-muted-foreground/50">•</span>

        {/* Progression-specific details */}
        {exercise.progressionType === 'Hypertrophy' && exercise.trainingMax ? (
          <>
            <span className="font-medium text-primary">
              TM: {exercise.trainingMax.value}{exercise.trainingMax.unit === 0 ? 'kg' : 'lbs'}
            </span>
            <span className="text-muted-foreground/50">•</span>
            <span>{exercise.isPrimary ? 'Primary' : 'Auxiliary'}</span>
            <span className="text-muted-foreground/50">•</span>
            <span>{exercise.baseSetsPerExercise} sets</span>
          </>
        ) : exercise.progressionType === 'RepsPerSet' && exercise.repRange ? (
          <>
            <span className="font-medium text-primary">
              {exercise.startingWeight}{exercise.weightUnit === 0 ? 'kg' : 'lbs'}
            </span>
            <span className="text-muted-foreground/50">•</span>
            <span>{exercise.repRange.minimum}-{exercise.repRange.target}-{exercise.repRange.maximum} reps</span>
            <span className="text-muted-foreground/50">•</span>
            <span>{exercise.currentSets}→{exercise.targetSets} sets</span>
          </>
        ) : null}

        {/* Day badge - only show when not in column view */}
        {!showOrder && (
          <>
            <span className="text-muted-foreground/50">•</span>
            <span className="inline-flex items-center px-1.5 py-0.5 rounded-md bg-blue-500/10 text-blue-600 dark:text-blue-400 font-medium">
              Day {exercise.assignedDay}
            </span>
          </>
        )}
      </div>
    </div>
  );
}
