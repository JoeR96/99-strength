import { useState, useMemo, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useExerciseLibrary } from "@/hooks/useWorkouts";
import {
  LinearSettingsForm,
  RepsPerSetSettingsForm,
  MinimalSetsSettingsForm,
  getEquipmentLabel,
  AMRAP_COMPATIBLE_EQUIPMENT,
  type ProgressionConfig,
  type ProgressionType,
} from "./SubstitutionConfigForms";
import {
  WeightUnit,
  type ExerciseDto,
  type ExerciseTemplate,
  type LinearProgressionDto,
  type RepsPerSetProgressionDto,
  type MinimalSetsProgressionDto,
} from "@/types/workout";


interface ExerciseSubstitutionConfigModalProps {
  exercise: ExerciseDto;
  isOpen: boolean;
  onClose: () => void;
  onSubstitute: (
    originalExercise: ExerciseDto,
    substituteTemplate: ExerciseTemplate,
    progressionConfig: ProgressionConfig
  ) => void;
}

export function ExerciseSubstitutionConfigModal({
  exercise,
  isOpen,
  onClose,
  onSubstitute,
}: ExerciseSubstitutionConfigModalProps) {
  const { data: library, isLoading } = useExerciseLibrary();
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedExercise, setSelectedExercise] =
    useState<ExerciseTemplate | null>(null);
  const [step, setStep] = useState<"select" | "configure">("select");

  // Get original progression info
  const originalProgression = exercise.progression;
  const isOriginalLinear = originalProgression.type === "Linear";
  const isOriginalRepsPerSet = originalProgression.type === "RepsPerSet";
  const isOriginalMinimalSets = originalProgression.type === "MinimalSets";

  const linearProg = isOriginalLinear
    ? (originalProgression as LinearProgressionDto)
    : null;
  const repsPerSetProg = isOriginalRepsPerSet
    ? (originalProgression as RepsPerSetProgressionDto)
    : null;
  const minimalSetsProg = isOriginalMinimalSets
    ? (originalProgression as MinimalSetsProgressionDto)
    : null;

  // Progression type state
  const [progressionType, setProgressionType] = useState<ProgressionType>(
    originalProgression.type
  );

  // Linear (Hypertrophy) progression state
  const [trainingMaxValue, setTrainingMaxValue] = useState<number>(
    linearProg?.trainingMax.value ?? 100
  );
  const [weightUnit, setWeightUnit] = useState<WeightUnit>(
    linearProg?.trainingMax.unit ?? WeightUnit.Kilograms
  );
  const [isPrimary, setIsPrimary] = useState<boolean>(
    linearProg?.useAmrap ?? true
  );
  const baseSets = linearProg?.baseSetsPerExercise ?? 3;

  // RepsPerSet progression state
  const [repRangeMin, setRepRangeMin] = useState<number>(
    repsPerSetProg?.repRange?.minimum ?? 8
  );
  const [repRangeMax, setRepRangeMax] = useState<number>(
    repsPerSetProg?.repRange?.maximum ?? 12
  );
  const [currentSets, setCurrentSets] = useState<number>(
    repsPerSetProg?.currentSetCount ?? 3
  );
  const [targetSets, setTargetSets] = useState<number>(
    repsPerSetProg?.targetSets ?? 5
  );
  const [startingWeight, setStartingWeight] = useState<number>(
    repsPerSetProg?.currentWeight ?? 50
  );

  // MinimalSets progression state
  const [minimalSetsWeight, setMinimalSetsWeight] = useState<number>(
    minimalSetsProg?.currentWeight ?? 0
  );
  const [targetTotalReps, setTargetTotalReps] = useState<number>(
    minimalSetsProg?.targetTotalReps ?? 40
  );
  const [minSets, setMinSets] = useState<number>(
    minimalSetsProg?.minimumSets ?? 3
  );
  const [maxSets, setMaxSets] = useState<number>(
    minimalSetsProg?.maximumSets ?? 6
  );
  const [minimalCurrentSets, setMinimalCurrentSets] = useState<number>(
    minimalSetsProg?.currentSetCount ?? 4
  );

  // Update weight unit from original exercise
  useEffect(() => {
    if (repsPerSetProg?.weightUnit) {
      setWeightUnit(
        repsPerSetProg.weightUnit.toLowerCase() === "pounds"
          ? WeightUnit.Pounds
          : WeightUnit.Kilograms
      );
    } else if (minimalSetsProg?.weightUnit) {
      setWeightUnit(
        minimalSetsProg.weightUnit.toLowerCase() === "pounds"
          ? WeightUnit.Pounds
          : WeightUnit.Kilograms
      );
    }
  }, [repsPerSetProg, minimalSetsProg]);

  // When selecting an exercise, suggest appropriate progression type
  useEffect(() => {
    if (selectedExercise) {
      const isBarbell = AMRAP_COMPATIBLE_EQUIPMENT.includes(
        selectedExercise.equipment
      );
      if (isBarbell && !isOriginalLinear) {
        // Suggest Linear for barbell exercises
        setProgressionType("Linear");
        // Suggest a training max based on original weight
        if (repsPerSetProg) {
          setTrainingMaxValue(
            Math.round((repsPerSetProg.currentWeight * 1.5) / 2.5) * 2.5
          );
        } else if (minimalSetsProg) {
          setTrainingMaxValue(
            Math.round((minimalSetsProg.currentWeight * 1.5) / 2.5) * 2.5
          );
        }
      } else if (!isBarbell && isOriginalLinear) {
        // Suggest RepsPerSet for non-barbell exercises
        setProgressionType("RepsPerSet");
        // Suggest starting weight as ~60% of training max
        if (linearProg) {
          setStartingWeight(
            Math.round((linearProg.trainingMax.value * 0.6) / 2.5) * 2.5
          );
        }
      }
    }
  }, [selectedExercise, isOriginalLinear, linearProg, repsPerSetProg, minimalSetsProg]);

  // Filter exercises based on search query
  const filteredExercises = useMemo(() => {
    if (!library?.templates) return [];

    const query = searchQuery.toLowerCase().trim();
    if (!query) return library.templates.slice(0, 20);

    return library.templates
      .filter(
        (template) =>
          template.name.toLowerCase().includes(query) ||
          template.description?.toLowerCase().includes(query)
      )
      .slice(0, 20);
  }, [library, searchQuery]);

  const handleSelectExercise = (template: ExerciseTemplate) => {
    setSelectedExercise(template);
    setStep("configure");
  };

  const handleBack = () => {
    setStep("select");
  };

  const handleSubstitute = () => {
    if (!selectedExercise) return;

    let config: ProgressionConfig;

    if (progressionType === "Linear") {
      config = {
        type: "Linear",
        trainingMaxValue,
        trainingMaxUnit: weightUnit,
        baseSets,
        isPrimary,
      };
    } else if (progressionType === "MinimalSets") {
      config = {
        type: "MinimalSets",
        weight: minimalSetsWeight,
        weightUnit,
        targetTotalReps,
        minSets,
        currentSets: minimalCurrentSets,
        maxSets,
      };
    } else {
      config = {
        type: "RepsPerSet",
        startingWeight,
        weightUnit,
        repRangeMin,
        repRangeMax,
        currentSets,
        targetSets,
      };
    }

    onSubstitute(exercise, selectedExercise, config);
    handleClose();
  };

  const handleClose = () => {
    setSearchQuery("");
    setSelectedExercise(null);
    setStep("select");
    onClose();
  };

  if (!isOpen) return null;

  const progressionDescriptions: Record<ProgressionType, string> = {
    Linear:
      "Uses training max percentages with AMRAP sets. Best for main compound lifts.",
    RepsPerSet:
      "Progress by adding sets and reps, then increase weight. Great for accessories.",
    MinimalSets:
      "Hit a total rep target in as few sets as possible. Ideal for bodyweight exercises.",
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <Card className="w-full max-w-2xl max-h-[90vh] overflow-hidden m-4 flex flex-col">
        {/* Header */}
        <div className="p-6 border-b">
          <div className="flex justify-between items-center">
            <div>
              <h2 className="text-xl font-bold">
                {step === "select" ? "Substitute Exercise" : "Configure Progression"}
              </h2>
              <p className="text-sm text-muted-foreground mt-1">
                {step === "select"
                  ? `Replace "${exercise.name}" with another exercise`
                  : `Configure "${selectedExercise?.name}"`}
              </p>
            </div>
            <Button variant="ghost" onClick={handleClose} className="text-2xl p-2">
              &times;
            </Button>
          </div>
        </div>

        {step === "select" ? (
          <>
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
                  <svg
                    className="h-6 w-6 animate-spin text-muted-foreground"
                    fill="none"
                    viewBox="0 0 24 24"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    />
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    />
                  </svg>
                  <span className="ml-2 text-muted-foreground">
                    Loading exercises...
                  </span>
                </div>
              ) : filteredExercises.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No exercises found matching "{searchQuery}"
                </div>
              ) : (
                <div className="space-y-2">
                  {filteredExercises.map((template) => (
                    <button
                      key={template.name}
                      onClick={() => handleSelectExercise(template)}
                      className="w-full p-3 rounded-lg border text-left transition-colors border-border hover:bg-muted/50 hover:border-primary/50"
                    >
                      <div className="flex items-center justify-between">
                        <div className="font-medium">{template.name}</div>
                        <span className="text-xs px-2 py-0.5 rounded bg-muted text-muted-foreground">
                          {getEquipmentLabel(template.equipment)}
                        </span>
                      </div>
                      {template.description && (
                        <div className="text-sm text-muted-foreground mt-1 line-clamp-2">
                          {template.description}
                        </div>
                      )}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </>
        ) : (
          <>
            {/* Configuration Step */}
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {/* Progression type selection */}
              <div>
                <label className="block text-sm font-semibold mb-3 text-foreground">
                  Progression Type
                </label>
                <div className="grid grid-cols-3 gap-3">
                  {(["Linear", "RepsPerSet", "MinimalSets"] as ProgressionType[]).map(
                    (type) => (
                      <button
                        key={type}
                        type="button"
                        onClick={() => setProgressionType(type)}
                        className={`px-4 py-3 text-sm font-medium rounded-xl border-2 transition-all ${
                          progressionType === type
                            ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
                            : "border-border hover:border-primary/50 hover:bg-muted/50 text-foreground"
                        }`}
                      >
                        {type === "Linear"
                          ? "Hypertrophy"
                          : type === "RepsPerSet"
                          ? "Reps/Set"
                          : "Minimal Sets"}
                      </button>
                    )
                  )}
                </div>
                <div className="mt-3 p-3 bg-muted/30 rounded-xl border border-border/50">
                  <p className="text-xs text-muted-foreground">
                    {progressionDescriptions[progressionType]}
                  </p>
                </div>
              </div>

              {/* Linear (Hypertrophy) Settings */}
              {progressionType === "Linear" && (
                <LinearSettingsForm
                  trainingMaxValue={trainingMaxValue}
                  setTrainingMaxValue={setTrainingMaxValue}
                  weightUnit={weightUnit}
                  setWeightUnit={setWeightUnit}
                  isPrimary={isPrimary}
                  setIsPrimary={setIsPrimary}
                />
              )}

              {/* RepsPerSet Settings */}
              {progressionType === "RepsPerSet" && (
                <RepsPerSetSettingsForm
                  repRangeMin={repRangeMin}
                  setRepRangeMin={setRepRangeMin}
                  repRangeMax={repRangeMax}
                  setRepRangeMax={setRepRangeMax}
                  startingWeight={startingWeight}
                  setStartingWeight={setStartingWeight}
                  weightUnit={weightUnit}
                  setWeightUnit={setWeightUnit}
                  currentSets={currentSets}
                  setCurrentSets={setCurrentSets}
                  targetSets={targetSets}
                  setTargetSets={setTargetSets}
                />
              )}

              {/* MinimalSets Settings */}
              {progressionType === "MinimalSets" && (
                <MinimalSetsSettingsForm
                  minimalSetsWeight={minimalSetsWeight}
                  setMinimalSetsWeight={setMinimalSetsWeight}
                  weightUnit={weightUnit}
                  setWeightUnit={setWeightUnit}
                  targetTotalReps={targetTotalReps}
                  setTargetTotalReps={setTargetTotalReps}
                  minSets={minSets}
                  setMinSets={setMinSets}
                  minimalCurrentSets={minimalCurrentSets}
                  setMinimalCurrentSets={setMinimalCurrentSets}
                  maxSets={maxSets}
                  setMaxSets={setMaxSets}
                />
              )}
            </div>

            {/* Actions */}
            <div className="p-4 border-t flex justify-between">
              <Button variant="outline" onClick={handleBack}>
                Back
              </Button>
              <div className="flex gap-3">
                <Button variant="outline" onClick={handleClose}>
                  Cancel
                </Button>
                <Button onClick={handleSubstitute}>Substitute Exercise</Button>
              </div>
            </div>
          </>
        )}

        {/* Actions for Select step */}
        {step === "select" && (
          <div className="p-4 border-t flex justify-end">
            <Button variant="outline" onClick={handleClose}>
              Cancel
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
