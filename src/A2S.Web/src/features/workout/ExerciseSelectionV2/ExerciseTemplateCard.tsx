import type { ExerciseTemplate, EquipmentType } from "@/types/workout";

interface ExerciseTemplateCardProps {
  template: ExerciseTemplate;
  onAdd: (template: ExerciseTemplate) => void;
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
 * Display an exercise template from the library with an "Add" button
 */
export function ExerciseTemplateCard({ template, onAdd }: ExerciseTemplateCardProps) {
  return (
    <div className="p-2.5 border border-border rounded-md hover:border-primary/50 transition-colors bg-card flex flex-col h-full">
      {/* Header with name and add button */}
      <div className="flex items-start justify-between gap-2 mb-2">
        <h4 className="font-medium text-sm leading-tight flex-1">{template.name}</h4>
        <button
          type="button"
          onClick={() => onAdd(template)}
          className="shrink-0 px-2 py-1 text-xs font-medium rounded-md bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
          aria-label={`Add ${template.name}`}
        >
          Add
        </button>
      </div>

      {/* Equipment badge */}
      <div className="mb-auto">
        <span className="inline-flex items-center px-2 py-0.5 text-xs rounded-md bg-muted text-muted-foreground">
          {getEquipmentLabel(template.equipment)}
        </span>
      </div>

      {/* Description */}
      {template.description && (
        <p className="text-xs text-muted-foreground mt-2 line-clamp-2">
          {template.description}
        </p>
      )}
    </div>
  );
}
