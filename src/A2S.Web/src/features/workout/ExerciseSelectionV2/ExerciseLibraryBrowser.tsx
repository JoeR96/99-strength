import { useState, useMemo } from "react";
import type { ExerciseTemplate, EquipmentType } from "@/types/workout";
import { ExerciseTemplateCard } from "./ExerciseTemplateCard";

interface ExerciseLibraryBrowserProps {
  templates: ExerciseTemplate[];
  onAddExercise: (template: ExerciseTemplate) => void;
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
 * Browser for the exercise library with search and filters
 */
export function ExerciseLibraryBrowser({
  templates,
  onAddExercise,
}: ExerciseLibraryBrowserProps) {
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedEquipment, setSelectedEquipment] = useState<EquipmentType | "all">("all");

  // Get unique equipment types from templates
  const availableEquipment = useMemo(() => {
    const equipmentSet = new Set(templates.map((t) => t.equipment));
    return Array.from(equipmentSet).sort((a, b) => a - b);
  }, [templates]);

  // Filter templates based on search and equipment
  const filteredTemplates = useMemo(() => {
    return templates.filter((template) => {
      // Search filter
      const matchesSearch = template.name
        .toLowerCase()
        .includes(searchQuery.toLowerCase()) ||
        template.description?.toLowerCase().includes(searchQuery.toLowerCase());

      // Equipment filter
      const matchesEquipment =
        selectedEquipment === "all" || template.equipment === selectedEquipment;

      return matchesSearch && matchesEquipment;
    });
  }, [templates, searchQuery, selectedEquipment]);

  return (
    <div className="space-y-4">
      {/* Search input */}
      <div>
        <input
          type="text"
          placeholder="Search exercises..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="w-full px-3 py-2 text-sm border border-border rounded-md bg-background focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
        />
      </div>

      {/* Equipment filter */}
      <div>
        <label className="text-sm font-medium mb-2 block">Filter by Equipment</label>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setSelectedEquipment("all")}
            className={`px-3 py-1.5 text-xs rounded-md transition-colors ${
              selectedEquipment === "all"
                ? "bg-primary text-primary-foreground"
                : "bg-muted hover:bg-muted/80"
            }`}
          >
            All ({templates.length})
          </button>
          {availableEquipment.map((equipment) => {
            const count = templates.filter((t) => t.equipment === equipment).length;
            return (
              <button
                key={equipment}
                type="button"
                onClick={() => setSelectedEquipment(equipment)}
                className={`px-3 py-1.5 text-xs rounded-md transition-colors ${
                  selectedEquipment === equipment
                    ? "bg-primary text-primary-foreground"
                    : "bg-muted hover:bg-muted/80"
                }`}
              >
                {getEquipmentLabel(equipment)} ({count})
              </button>
            );
          })}
        </div>
      </div>

      {/* Results count */}
      <div className="text-sm text-muted-foreground">
        {filteredTemplates.length === templates.length ? (
          <span>Showing all {templates.length} exercises</span>
        ) : (
          <span>
            Showing {filteredTemplates.length} of {templates.length} exercises
          </span>
        )}
      </div>

      {/* Exercise grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3 max-h-[600px] overflow-y-auto pr-2">
        {filteredTemplates.length > 0 ? (
          filteredTemplates.map((template) => (
            <ExerciseTemplateCard
              key={template.name}
              template={template}
              onAdd={onAddExercise}
            />
          ))
        ) : (
          <div className="col-span-full text-center py-8 text-muted-foreground">
            No exercises found matching your criteria
          </div>
        )}
      </div>
    </div>
  );
}
