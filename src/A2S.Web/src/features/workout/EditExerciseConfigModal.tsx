import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  WeightUnit,
  type ExerciseDto,
  type LinearProgressionDto,
  type RepsPerSetProgressionDto,
  type MinimalSetsProgressionDto,
  type ProgressionConfigRequest,
} from "@/types/workout";

interface EditExerciseConfigModalProps {
  exercise: ExerciseDto;
  isOpen: boolean;
  onClose: () => void;
  onSave: (exerciseId: string, config: ExerciseConfigUpdate) => Promise<void>;
  onChangeProgression: (exerciseId: string, config: ProgressionConfigRequest) => Promise<void>;
}

export interface ExerciseConfigUpdate {
  // For Linear progression
  trainingMaxValue?: number;
  trainingMaxUnit?: WeightUnit;
  // For RepsPerSet/MinimalSets
  weightValue?: number;
  weightUnit?: WeightUnit;
  // For RepsPerSet only
  isUnilateral?: boolean;
}

type ProgressionType = "Linear" | "RepsPerSet";

export function EditExerciseConfigModal({
  exercise,
  isOpen,
  onClose,
  onSave,
  onChangeProgression,
}: EditExerciseConfigModalProps) {
  const [isSaving, setIsSaving] = useState(false);

  // Determine current progression type
  const currentType = exercise.progression.type as ProgressionType;
  const isLinear = currentType === "Linear";
  const isRepsPerSet = currentType === "RepsPerSet";

  const linearProg = isLinear
    ? (exercise.progression as LinearProgressionDto)
    : null;
  const repsPerSetProg = isRepsPerSet
    ? (exercise.progression as RepsPerSetProgressionDto)
    : null;

  // Whether user wants to swap to the other type
  const [wantSwap, setWantSwap] = useState(false);

  // Linear fields (for editing current Linear or swapping TO Linear)
  const [trainingMaxValue, setTrainingMaxValue] = useState<number>(
    linearProg?.trainingMax.value ?? 100
  );
  const [linearWeightUnit, setLinearWeightUnit] = useState<WeightUnit>(
    linearProg?.trainingMax.unit ?? WeightUnit.Kilograms
  );

  // RepsPerSet fields (for editing current RPS or swapping TO RPS)
  const [weightValue, setWeightValue] = useState<number>(
    repsPerSetProg?.currentWeight ?? 50
  );
  const [rpsWeightUnit, setRpsWeightUnit] = useState<WeightUnit>(WeightUnit.Kilograms);
  const [repRangeMin, setRepRangeMin] = useState<number>(
    repsPerSetProg?.repRange?.minimum ?? 8
  );
  const [repRangeMax, setRepRangeMax] = useState<number>(
    repsPerSetProg?.repRange?.maximum ?? 12
  );
  const [repRangeTarget, setRepRangeTarget] = useState<number>(
    repsPerSetProg?.repRange?.target ?? 10
  );
  const [targetSets, setTargetSets] = useState<number>(
    repsPerSetProg?.targetSets ?? 5
  );
  const [isUnilateral, setIsUnilateral] = useState<boolean>(
    repsPerSetProg?.isUnilateral ?? false
  );

  // Reset form when exercise changes
  useEffect(() => {
    setWantSwap(false);
    if (linearProg) {
      setTrainingMaxValue(linearProg.trainingMax.value);
      setLinearWeightUnit(linearProg.trainingMax.unit);
      // Pre-fill RPS fields for potential swap: ~60% of TM
      setWeightValue(Math.round((linearProg.trainingMax.value * 0.6) / 2.5) * 2.5);
      setRpsWeightUnit(linearProg.trainingMax.unit);
      setRepRangeMin(8);
      setRepRangeTarget(10);
      setRepRangeMax(12);
      setTargetSets(5);
      setIsUnilateral(false);
    }
    if (repsPerSetProg) {
      setWeightValue(repsPerSetProg.currentWeight);
      setRepRangeMin(repsPerSetProg.repRange?.minimum ?? 8);
      setRepRangeTarget(repsPerSetProg.repRange?.target ?? 10);
      setRepRangeMax(repsPerSetProg.repRange?.maximum ?? 12);
      setTargetSets(repsPerSetProg.targetSets ?? 5);
      setIsUnilateral(repsPerSetProg.isUnilateral ?? false);
      setRpsWeightUnit(
        repsPerSetProg.weightUnit?.toLowerCase() === "pounds"
          ? WeightUnit.Pounds
          : WeightUnit.Kilograms
      );
      // Pre-fill Linear fields for potential swap: ~150% of current weight
      setTrainingMaxValue(Math.round((repsPerSetProg.currentWeight * 1.5) / 2.5) * 2.5);
      setLinearWeightUnit(
        repsPerSetProg.weightUnit?.toLowerCase() === "pounds"
          ? WeightUnit.Pounds
          : WeightUnit.Kilograms
      );
    }
  }, [exercise, linearProg, repsPerSetProg]);

  const handleSave = async () => {
    setIsSaving(true);
    try {
      if (wantSwap) {
        // Swap progression type via substitute API
        let config: ProgressionConfigRequest;
        if (isLinear) {
          // Swapping from Linear -> RepsPerSet
          config = {
            type: "RepsPerSet",
            startingWeight: weightValue,
            weightUnit: rpsWeightUnit,
            repRangeMinimum: repRangeMin,
            repRangeTarget: repRangeTarget,
            repRangeMaximum: repRangeMax,
            targetSets: targetSets,
            isUnilateral: isUnilateral,
          };
        } else {
          // Swapping from RepsPerSet -> Linear
          config = {
            type: "Linear",
            trainingMaxValue: trainingMaxValue,
            trainingMaxUnit: linearWeightUnit,
            useAmrap: true,
            baseSetsPerExercise: 4,
          };
        }
        await onChangeProgression(exercise.id, config);
      } else if (isRepsPerSet && repsPerSetProg && (
        repRangeMin !== (repsPerSetProg.repRange?.minimum ?? 8) ||
        repRangeMax !== (repsPerSetProg.repRange?.maximum ?? 12) ||
        repRangeTarget !== (repsPerSetProg.repRange?.target ?? 10)
      )) {
        // Rep range changed — use substitute API to recreate the progression
        const config: ProgressionConfigRequest = {
          type: "RepsPerSet",
          startingWeight: weightValue,
          weightUnit: rpsWeightUnit,
          repRangeMinimum: repRangeMin,
          repRangeTarget: repRangeTarget,
          repRangeMaximum: repRangeMax,
          targetSets: repsPerSetProg.targetSets ?? 5,
          isUnilateral: isUnilateral,
        };
        await onChangeProgression(exercise.id, config);
      } else {
        // Edit within same progression type (weight/TM/unilateral only)
        const config: ExerciseConfigUpdate = {};
        if (isLinear) {
          config.trainingMaxValue = trainingMaxValue;
          config.trainingMaxUnit = linearWeightUnit;
        } else if (isRepsPerSet) {
          config.weightValue = weightValue;
          config.weightUnit = rpsWeightUnit;
          config.isUnilateral = isUnilateral;
        }
        await onSave(exercise.id, config);
      }
      onClose();
    } catch {
      // Error handling is done in parent
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen) return null;

  const swapTargetLabel = isLinear ? "Reps Per Set" : "A2S Hypertrophy";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />

      {/* Modal */}
      <div className="relative bg-background rounded-lg shadow-lg w-full max-w-md mx-4 max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 border-b border-border">
          <h2 className="text-lg font-semibold">Edit {exercise.name}</h2>
          <p className="text-sm text-muted-foreground mt-1">
            {wantSwap
              ? `Swap to ${swapTargetLabel}`
              : `${currentType} Progression`}
          </p>
        </div>

        {/* Content */}
        <div className="p-4 space-y-4 overflow-y-auto max-h-[60vh]">
          {/* ===================== */}
          {/* EDITING LINEAR (no swap) */}
          {/* ===================== */}
          {isLinear && !wantSwap && (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">
                  Training Max
                </label>
                <div className="flex gap-2">
                  <Input
                    type="number"
                    value={trainingMaxValue}
                    onChange={(e) =>
                      setTrainingMaxValue(parseFloat(e.target.value) || 0)
                    }
                    className="flex-1"
                    step="2.5"
                  />
                  <select
                    value={linearWeightUnit}
                    onChange={(e) =>
                      setLinearWeightUnit(parseInt(e.target.value) as WeightUnit)
                    }
                    className="px-3 py-2 border border-input rounded-md bg-background"
                  >
                    <option value={WeightUnit.Kilograms}>kg</option>
                    <option value={WeightUnit.Pounds}>lbs</option>
                  </select>
                </div>
              </div>

              {linearProg && (
                <div className="text-sm text-muted-foreground bg-muted/50 rounded p-3">
                  <div className="font-medium mb-1">Current Config:</div>
                  <div>Sets: {linearProg.baseSetsPerExercise}</div>
                  <div>AMRAP: {linearProg.useAmrap ? "Yes" : "No"}</div>
                </div>
              )}
            </div>
          )}

          {/* ===================== */}
          {/* EDITING REPS PER SET (no swap) */}
          {/* ===================== */}
          {isRepsPerSet && !wantSwap && (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">
                  Current Weight
                </label>
                <div className="flex gap-2">
                  <Input
                    type="number"
                    value={weightValue}
                    onChange={(e) =>
                      setWeightValue(parseFloat(e.target.value) || 0)
                    }
                    className="flex-1"
                    step="2.5"
                  />
                  <select
                    value={rpsWeightUnit}
                    onChange={(e) =>
                      setRpsWeightUnit(parseInt(e.target.value) as WeightUnit)
                    }
                    className="px-3 py-2 border border-input rounded-md bg-background"
                  >
                    <option value={WeightUnit.Kilograms}>kg</option>
                    <option value={WeightUnit.Pounds}>lbs</option>
                  </select>
                </div>
              </div>

              {/* Rep Range */}
              <div>
                <label className="block text-sm font-medium mb-1">
                  Rep Range
                </label>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="text-xs text-muted-foreground">Min</label>
                    <Input
                      type="number"
                      value={repRangeMin}
                      onChange={(e) => setRepRangeMin(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Target</label>
                    <Input
                      type="number"
                      value={repRangeTarget}
                      onChange={(e) => setRepRangeTarget(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Max</label>
                    <Input
                      type="number"
                      value={repRangeMax}
                      onChange={(e) => setRepRangeMax(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                </div>
              </div>

              {/* Unilateral Toggle */}
              <div className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
                <div>
                  <div className="font-medium text-sm">Unilateral</div>
                  <div className="text-xs text-muted-foreground">
                    One side at a time (sets doubled)
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setIsUnilateral(!isUnilateral)}
                  className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                    isUnilateral ? "bg-primary" : "bg-muted"
                  }`}
                >
                  <span
                    className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                      isUnilateral ? "translate-x-6" : "translate-x-1"
                    }`}
                  />
                </button>
              </div>

              {repsPerSetProg && (
                <div className="text-sm text-muted-foreground bg-muted/50 rounded p-3">
                  <div className="font-medium mb-1">Current Config:</div>
                  <div>
                    Sets: {repsPerSetProg.currentSetCount}/{repsPerSetProg.targetSets}
                    {isUnilateral && " per side"}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* ===================== */}
          {/* SWAPPING LINEAR -> REPS PER SET */}
          {/* ===================== */}
          {isLinear && wantSwap && (
            <div className="space-y-4">
              <div className="p-3 bg-blue-50 dark:bg-blue-950/30 rounded-lg border border-blue-200 dark:border-blue-800">
                <p className="text-sm text-blue-700 dark:text-blue-300">
                  This will replace the A2S Hypertrophy progression with Reps Per Set.
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">
                  Starting Weight
                </label>
                <div className="flex gap-2">
                  <Input
                    type="number"
                    value={weightValue}
                    onChange={(e) =>
                      setWeightValue(parseFloat(e.target.value) || 0)
                    }
                    className="flex-1"
                    step="2.5"
                  />
                  <select
                    value={rpsWeightUnit}
                    onChange={(e) =>
                      setRpsWeightUnit(parseInt(e.target.value) as WeightUnit)
                    }
                    className="px-3 py-2 border border-input rounded-md bg-background"
                  >
                    <option value={WeightUnit.Kilograms}>kg</option>
                    <option value={WeightUnit.Pounds}>lbs</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">
                  Rep Range
                </label>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="text-xs text-muted-foreground">Min</label>
                    <Input
                      type="number"
                      value={repRangeMin}
                      onChange={(e) => setRepRangeMin(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Target</label>
                    <Input
                      type="number"
                      value={repRangeTarget}
                      onChange={(e) => setRepRangeTarget(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground">Max</label>
                    <Input
                      type="number"
                      value={repRangeMax}
                      onChange={(e) => setRepRangeMax(Number(e.target.value))}
                      min="1"
                      max="30"
                    />
                  </div>
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">
                  Target Sets
                </label>
                <Input
                  type="number"
                  value={targetSets}
                  onChange={(e) => setTargetSets(Number(e.target.value))}
                  min="1"
                  max="10"
                />
              </div>

              {/* Unilateral Toggle */}
              <div className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
                <div>
                  <div className="font-medium text-sm">Unilateral</div>
                  <div className="text-xs text-muted-foreground">
                    One side at a time (sets doubled)
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setIsUnilateral(!isUnilateral)}
                  className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                    isUnilateral ? "bg-primary" : "bg-muted"
                  }`}
                >
                  <span
                    className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                      isUnilateral ? "translate-x-6" : "translate-x-1"
                    }`}
                  />
                </button>
              </div>
            </div>
          )}

          {/* ===================== */}
          {/* SWAPPING REPS PER SET -> LINEAR */}
          {/* ===================== */}
          {isRepsPerSet && wantSwap && (
            <div className="space-y-4">
              <div className="p-3 bg-blue-50 dark:bg-blue-950/30 rounded-lg border border-blue-200 dark:border-blue-800">
                <p className="text-sm text-blue-700 dark:text-blue-300">
                  This will replace Reps Per Set with A2S Hypertrophy (Linear) progression.
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">
                  Training Max
                </label>
                <div className="flex gap-2">
                  <Input
                    type="number"
                    value={trainingMaxValue}
                    onChange={(e) =>
                      setTrainingMaxValue(parseFloat(e.target.value) || 0)
                    }
                    className="flex-1"
                    step="2.5"
                  />
                  <select
                    value={linearWeightUnit}
                    onChange={(e) =>
                      setLinearWeightUnit(parseInt(e.target.value) as WeightUnit)
                    }
                    className="px-3 py-2 border border-input rounded-md bg-background"
                  >
                    <option value={WeightUnit.Kilograms}>kg</option>
                    <option value={WeightUnit.Pounds}>lbs</option>
                  </select>
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  Should be ~90-95% of your 1RM
                </p>
              </div>
            </div>
          )}

          {/* ===================== */}
          {/* SWAP TOGGLE BUTTON */}
          {/* ===================== */}
          <div className="pt-2 border-t border-border">
            <button
              type="button"
              onClick={() => setWantSwap(!wantSwap)}
              className={`w-full px-4 py-3 text-sm font-medium rounded-lg border-2 transition-all flex items-center justify-center gap-2 ${
                wantSwap
                  ? "border-orange-400 bg-orange-50 text-orange-700 dark:bg-orange-950/30 dark:text-orange-300 dark:border-orange-700"
                  : "border-border hover:border-primary/50 hover:bg-muted/50 text-muted-foreground"
              }`}
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
              {wantSwap
                ? `Cancel — keep ${currentType}`
                : `Swap to ${swapTargetLabel}`}
            </button>
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-border flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={isSaving}
            variant={wantSwap ? "destructive" : "default"}
          >
            {isSaving
              ? "Saving..."
              : wantSwap
              ? `Swap to ${swapTargetLabel}`
              : "Save Changes"}
          </Button>
        </div>
      </div>
    </div>
  );
}
