import { useState, useMemo } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useExerciseLibrary } from "@/hooks/useWorkouts";
import { EquipmentType, type ExerciseDto, type ExerciseTemplate, type LinearProgressionDto } from "@/types/workout";

interface ExerciseSubstitutionModalProps {
  exercise: ExerciseDto;
  isOpen: boolean;
  onClose: () => void;
  onTemporarySubstitute: (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => void;
  onPermanentSubstitute: (originalExercise: ExerciseDto, substituteTemplate: ExerciseTemplate, repsConfig?: RepsPerSetConfig) => void;
}

// Configuration for exercises that need RepsPerSet progression
export interface RepsPerSetConfig {
  targetReps: number;
  minReps: number;
  maxReps: number;
  sets: number;
  startingWeight: number;
}

type SubstitutionType = "temporary" | "permanent" | null;

// Equipment types that work well with A2S Linear/AMRAP progression
const AMRAP_COMPATIBLE_EQUIPMENT = [
  EquipmentType.Barbell,
  EquipmentType.SmithMachine,
];

// Equipment types that require RepsPerSet progression (no AMRAP)
const REPS_PER_SET_EQUIPMENT = [
  EquipmentType.Dumbbell,
  EquipmentType.Cable,
  EquipmentType.Machine,
];

function getEquipmentLabel(equipment: EquipmentType): string {
  switch (equipment) {
    case EquipmentType.Barbell: return "Barbell";
    case EquipmentType.Dumbbell: return "Dumbbell";
    case EquipmentType.Cable: return "Cable";
    case EquipmentType.Machine: return "Machine";
    case EquipmentType.Bodyweight: return "Bodyweight";
    case EquipmentType.SmithMachine: return "Smith Machine";
    default: return "Unknown";
  }
}

export function ExerciseSubstitutionModal({
  exercise,
  isOpen,
  onClose,
  onTemporarySubstitute,
  onPermanentSubstitute,
}: ExerciseSubstitutionModalProps) {
  const { data: library, isLoading } = useExerciseLibrary();
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedExercise, setSelectedExercise] = useState<ExerciseTemplate | null>(null);
  const [substitutionType, setSubstitutionType] = useState<SubstitutionType>(null);

  // RepsPerSet configuration state
  const [repsConfig, setRepsConfig] = useState<RepsPerSetConfig>({
    targetReps: 10,
    minReps: 8,
    maxReps: 12,
    sets: 3,
    startingWeight: 20,
  });

  // Check if original exercise uses Linear progression (AMRAP)
  const isOriginalLinear = exercise.progression.type === "Linear";
  const originalLinearProg = isOriginalLinear ? (exercise.progression as LinearProgressionDto) : null;

  // Check if the selected exercise requires switching to RepsPerSet
  const requiresProgressionChange = useMemo(() => {
    if (!selectedExercise || !isOriginalLinear) return false;

    // If switching from AMRAP-compatible to RepsPerSet equipment
    const originalIsAmrapCompatible = AMRAP_COMPATIBLE_EQUIPMENT.includes(exercise.equipment);
    const newRequiresRepsPerSet = REPS_PER_SET_EQUIPMENT.includes(selectedExercise.equipment);

    return originalIsAmrapCompatible && newRequiresRepsPerSet;
  }, [selectedExercise, exercise.equipment, isOriginalLinear]);

  // Initialize starting weight based on original exercise
  useMemo(() => {
    if (requiresProgressionChange && originalLinearProg) {
      // Suggest starting weight as ~60-70% of training max for dumbbell/cable/machine
      const suggestedWeight = Math.round(originalLinearProg.trainingMax.value * 0.6 / 2.5) * 2.5;
      setRepsConfig(prev => ({
        ...prev,
        startingWeight: suggestedWeight > 0 ? suggestedWeight : 20,
        sets: originalLinearProg.baseSetsPerExercise || 3,
      }));
    }
  }, [requiresProgressionChange, originalLinearProg]);

  // Filter exercises based on search query
  const filteredExercises = useMemo(() => {
    if (!library?.templates) return [];

    const query = searchQuery.toLowerCase().trim();
    if (!query) return library.templates.slice(0, 20); // Show first 20 when no search

    return library.templates.filter(
      (template) =>
        template.name.toLowerCase().includes(query) ||
        template.description?.toLowerCase().includes(query)
    ).slice(0, 20);
  }, [library, searchQuery]);

  const handleSubstitute = () => {
    if (!selectedExercise || !substitutionType) return;

    const configToPass = requiresProgressionChange ? repsConfig : undefined;

    if (substitutionType === "temporary") {
      onTemporarySubstitute(exercise, selectedExercise, configToPass);
    } else {
      onPermanentSubstitute(exercise, selectedExercise, configToPass);
    }
    handleClose();
  };

  const handleClose = () => {
    setSearchQuery("");
    setSelectedExercise(null);
    setSubstitutionType(null);
    setRepsConfig({
      targetReps: 10,
      minReps: 8,
      maxReps: 12,
      sets: 3,
      startingWeight: 20,
    });
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <Card className="w-full max-w-2xl max-h-[85vh] overflow-hidden m-4 flex flex-col">
        {/* Header */}
        <div className="p-6 border-b">
          <div className="flex justify-between items-center">
            <div>
              <h2 className="text-xl font-bold">Substitute Exercise</h2>
              <p className="text-sm text-muted-foreground mt-1">
                Replace "{exercise.name}" with another exercise
              </p>
            </div>
            <Button variant="ghost" onClick={handleClose} className="text-2xl p-2">
              &times;
            </Button>
          </div>
        </div>

        {/* Search */}
        <div className="p-4 border-b">
          <Input
            type="text"
            placeholder="Search exercises..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full"
            autoFocus
          />
        </div>

        {/* Exercise List */}
        <div className="flex-1 overflow-y-auto p-4">
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <svg className="h-6 w-6 animate-spin text-muted-foreground" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
              </svg>
              <span className="ml-2 text-muted-foreground">Loading exercises...</span>
            </div>
          ) : filteredExercises.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              No exercises found matching "{searchQuery}"
            </div>
          ) : (
            <div className="space-y-2">
              {filteredExercises.map((template) => {
                const needsProgressionSwitch = isOriginalLinear &&
                  AMRAP_COMPATIBLE_EQUIPMENT.includes(exercise.equipment) &&
                  REPS_PER_SET_EQUIPMENT.includes(template.equipment);

                return (
                  <button
                    key={template.name}
                    onClick={() => setSelectedExercise(template)}
                    className={`w-full p-3 rounded-lg border text-left transition-colors ${
                      selectedExercise?.name === template.name
                        ? "border-primary bg-primary/10"
                        : "border-border hover:bg-muted/50"
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="font-medium">{template.name}</div>
                      <div className="flex items-center gap-2">
                        <span className="text-xs px-2 py-0.5 rounded bg-muted text-muted-foreground">
                          {getEquipmentLabel(template.equipment)}
                        </span>
                        {needsProgressionSwitch && (
                          <span className="text-xs px-2 py-0.5 rounded bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400">
                            Reps-based
                          </span>
                        )}
                      </div>
                    </div>
                    {template.description && (
                      <div className="text-sm text-muted-foreground mt-1 line-clamp-2">
                        {template.description}
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* Progression Change Warning */}
        {selectedExercise && requiresProgressionChange && (
          <div className="p-4 border-t bg-yellow-50 dark:bg-yellow-950/30">
            <div className="flex items-start gap-3">
              <svg className="h-5 w-5 text-yellow-600 dark:text-yellow-400 flex-shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              <div className="flex-1">
                <p className="font-medium text-yellow-800 dark:text-yellow-200">
                  Progression Type Change Required
                </p>
                <p className="text-sm text-yellow-700 dark:text-yellow-300 mt-1">
                  {getEquipmentLabel(selectedExercise.equipment)} exercises don't work well with AMRAP progression.
                  This exercise will use <strong>Reps Per Set</strong> progression instead.
                </p>

                {/* Reps Configuration */}
                <div className="mt-4 grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs font-medium text-yellow-700 dark:text-yellow-300">Sets</label>
                    <Input
                      type="number"
                      value={repsConfig.sets}
                      onChange={(e) => setRepsConfig(prev => ({ ...prev, sets: parseInt(e.target.value) || 3 }))}
                      className="mt-1 h-8 bg-white dark:bg-zinc-800"
                      min={1}
                      max={10}
                    />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-yellow-700 dark:text-yellow-300">Target Reps</label>
                    <Input
                      type="number"
                      value={repsConfig.targetReps}
                      onChange={(e) => setRepsConfig(prev => ({ ...prev, targetReps: parseInt(e.target.value) || 10 }))}
                      className="mt-1 h-8 bg-white dark:bg-zinc-800"
                      min={1}
                      max={30}
                    />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-yellow-700 dark:text-yellow-300">Min Reps</label>
                    <Input
                      type="number"
                      value={repsConfig.minReps}
                      onChange={(e) => setRepsConfig(prev => ({ ...prev, minReps: parseInt(e.target.value) || 8 }))}
                      className="mt-1 h-8 bg-white dark:bg-zinc-800"
                      min={1}
                      max={repsConfig.targetReps}
                    />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-yellow-700 dark:text-yellow-300">Max Reps</label>
                    <Input
                      type="number"
                      value={repsConfig.maxReps}
                      onChange={(e) => setRepsConfig(prev => ({ ...prev, maxReps: parseInt(e.target.value) || 12 }))}
                      className="mt-1 h-8 bg-white dark:bg-zinc-800"
                      min={repsConfig.targetReps}
                      max={50}
                    />
                  </div>
                  <div className="col-span-2">
                    <label className="text-xs font-medium text-yellow-700 dark:text-yellow-300">
                      Starting Weight ({originalLinearProg?.trainingMax.unit === 1 ? 'kg' : 'lbs'})
                    </label>
                    <Input
                      type="number"
                      value={repsConfig.startingWeight}
                      onChange={(e) => setRepsConfig(prev => ({ ...prev, startingWeight: parseFloat(e.target.value) || 20 }))}
                      className="mt-1 h-8 bg-white dark:bg-zinc-800"
                      min={0}
                      step={2.5}
                    />
                    <p className="text-xs text-yellow-600 dark:text-yellow-400 mt-1">
                      Suggested: ~60% of your Training Max ({originalLinearProg?.trainingMax.value ?? 0} {originalLinearProg?.trainingMax.unit === 1 ? 'kg' : 'lbs'})
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Substitution Type Selection */}
        {selectedExercise && (
          <div className="p-4 border-t bg-muted/30">
            <p className="text-sm font-medium mb-3">
              Replacing with: <span className="text-primary">{selectedExercise.name}</span>
              {requiresProgressionChange && (
                <span className="ml-2 text-xs text-yellow-600 dark:text-yellow-400">
                  (Reps Per Set: {repsConfig.sets} × {repsConfig.minReps}-{repsConfig.targetReps}-{repsConfig.maxReps} @ {repsConfig.startingWeight}{originalLinearProg?.trainingMax.unit === 1 ? 'kg' : 'lbs'})
                </span>
              )}
            </p>
            <div className="flex gap-3">
              <button
                onClick={() => setSubstitutionType("temporary")}
                className={`flex-1 p-3 rounded-lg border text-left transition-colors ${
                  substitutionType === "temporary"
                    ? "border-primary bg-primary/10"
                    : "border-border hover:bg-muted/50"
                }`}
              >
                <div className="font-medium flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  This Session Only
                </div>
                <div className="text-xs text-muted-foreground mt-1">
                  Use this exercise just for today's workout
                </div>
              </button>
              <button
                onClick={() => setSubstitutionType("permanent")}
                className={`flex-1 p-3 rounded-lg border text-left transition-colors ${
                  substitutionType === "permanent"
                    ? "border-primary bg-primary/10"
                    : "border-border hover:bg-muted/50"
                }`}
              >
                <div className="font-medium flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                  Permanent
                </div>
                <div className="text-xs text-muted-foreground mt-1">
                  Replace in entire program going forward
                </div>
              </button>
            </div>
          </div>
        )}

        {/* Actions */}
        <div className="p-4 border-t flex justify-end gap-3">
          <Button variant="outline" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSubstitute}
            disabled={!selectedExercise || !substitutionType}
          >
            Substitute Exercise
          </Button>
        </div>
      </Card>
    </div>
  );
}
