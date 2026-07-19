import { useState, useEffect } from "react";
import type { SelectedExercise, DayNumber, ProgramVariant, WeightUnit } from "@/types/workout";
import { A2SProgressionType as ProgressionTypeEnum, WeightUnit as WeightUnitEnum, ExerciseCategory } from "@/types/workout";
import { ExerciseConfigFields } from "./ExerciseConfigFields";

// Extended type to include MinimalSets
type ProgressionType = 'Linear' | 'RepsPerSet' | 'MinimalSets';

interface ExerciseConfigDialogProps {
  exercise: SelectedExercise | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (id: string, updates: Partial<Omit<SelectedExercise, "id" | "template">>) => void;
  programVariant: ProgramVariant;
}

/**
 * Get available days based on program variant
 */
function getAvailableDays(variant: ProgramVariant): DayNumber[] {
  const days: DayNumber[] = [1, 2, 3, 4];
  if (variant >= 5) days.push(5);
  if (variant >= 6) days.push(6);
  return days;
}

/**
 * Dialog for configuring exercise progression type and day assignment
 * Supports all 3 A2S progression types: Linear (Hypertrophy), RepsPerSet, and MinimalSets
 */
export function ExerciseConfigDialog({
  exercise,
  isOpen,
  onClose,
  onSave,
  programVariant,
}: ExerciseConfigDialogProps) {
  const [progressionType, setProgressionType] = useState<ProgressionType>('RepsPerSet');
  const [assignedDay, setAssignedDay] = useState<DayNumber>(1);

  // Linear (Hypertrophy) progression state
  const [trainingMaxValue, setTrainingMaxValue] = useState<number>(100);
  const [weightUnit, setWeightUnit] = useState<WeightUnit>(WeightUnitEnum.Kilograms);
  const [isPrimary, setIsPrimary] = useState<boolean>(true);

  // RepsPerSet progression state
  const [isUnilateral, setIsUnilateral] = useState<boolean>(false);
  const [repRangeMin, setRepRangeMin] = useState<number>(8);
  const [repRangeMax, setRepRangeMax] = useState<number>(12);
  const [currentSets, setCurrentSets] = useState<number>(3);
  const [targetSets, setTargetSets] = useState<number>(5);
  // MinimalSets progression state
  const [targetTotalReps, setTargetTotalReps] = useState<number>(40);
  const [minSets, setMinSets] = useState<number>(3);
  const [maxSets, setMaxSets] = useState<number>(6);
  const [minimalCurrentSets, setMinimalCurrentSets] = useState<number>(3);

  // Update form when exercise changes
  useEffect(() => {
    if (exercise) {
      setProgressionType(exercise.progressionType as ProgressionType);
      setAssignedDay(exercise.assignedDay);

      // Linear (Hypertrophy) progression
      if (exercise.trainingMax) {
        setTrainingMaxValue(exercise.trainingMax.value);
        setWeightUnit(exercise.trainingMax.unit);
      }
      if (exercise.isPrimary !== undefined) setIsPrimary(exercise.isPrimary);

      // RepsPerSet progression
      if (exercise.repRange) {
        setRepRangeMin(exercise.repRange.minimum);
        setRepRangeMax(exercise.repRange.maximum);
      }
      setIsUnilateral(exercise.isUnilateral ?? false);
      if (exercise.currentSets) setCurrentSets(exercise.currentSets);
      if (exercise.targetSets) setTargetSets(exercise.targetSets);
      if (exercise.weightUnit !== undefined) setWeightUnit(exercise.weightUnit);

      // MinimalSets progression (reuse currentSets/targetSets as min/max)
      if (exercise.targetTotalReps != null) setTargetTotalReps(exercise.targetTotalReps);
      if (exercise.currentSets) {
        setMinSets(exercise.currentSets);
        setMinimalCurrentSets(exercise.currentSets);
      }
      if (exercise.targetSets) setMaxSets(exercise.targetSets);
    }
  }, [exercise]);

  if (!isOpen || !exercise) return null;

  const handleSave = () => {
    let progressionUpdates;

    if (progressionType === ProgressionTypeEnum.Linear) {
      progressionUpdates = {
        progressionType: progressionType as 'Linear',
        assignedDay,
        category: ExerciseCategory.MainLift,
        trainingMax: { value: trainingMaxValue, unit: weightUnit },
        isPrimary,
        isUnilateral: undefined,
        repRange: undefined,
        currentSets: undefined,
        targetSets: undefined,
        startingWeight: undefined,
      };
    } else if (progressionType === 'MinimalSets') {
      progressionUpdates = {
        progressionType: 'MinimalSets' as const,
        assignedDay,
        category: ExerciseCategory.Accessory,
        isUnilateral: undefined,
        startingWeight: undefined,
        weightUnit,
        currentSets: minimalCurrentSets,
        targetSets: maxSets,
        repRange: {
          minimum: Math.floor(targetTotalReps / maxSets),
          maximum: Math.ceil(targetTotalReps / minSets),
        },
        targetTotalReps,
        trainingMax: undefined,
        isPrimary: undefined,
        baseSetsPerExercise: undefined,
      };
    } else {
      progressionUpdates = {
        progressionType: progressionType as 'RepsPerSet',
        assignedDay,
        category: ExerciseCategory.Accessory,
        isUnilateral,
        repRange: {
          minimum: repRangeMin,
          maximum: repRangeMax,
        },
        currentSets,
        targetSets,
        startingWeight: undefined,
        weightUnit,
        trainingMax: undefined,
        isPrimary: undefined,
        baseSetsPerExercise: undefined,
      };
    }

    onSave(exercise.id, progressionUpdates);
    onClose();
  };

  const availableDays = getAvailableDays(programVariant);

  const progressionDescriptions: Record<ProgressionType, string> = {
    Linear: "Uses training max percentages with AMRAP sets. Best for main compound lifts.",
    RepsPerSet: "Progress by adding sets and reps, then increase weight. Great for accessories.",
    MinimalSets: "Hit a total rep target in as few sets as possible. Ideal for bodyweight exercises.",
  };

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/60 backdrop-blur-sm z-40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Dialog */}
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div
          className="bg-card text-card-foreground rounded-2xl shadow-xl w-full max-w-2xl flex flex-col max-h-[90vh] border border-border/50"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-border">
            <div>
              <h2 className="text-xl font-semibold text-foreground">Configure Exercise</h2>
              <p className="text-sm text-muted-foreground mt-1">{exercise.template.name}</p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="p-2 rounded-xl hover:bg-muted transition-colors"
              aria-label="Close dialog"
            >
              <svg
                className="w-5 h-5 text-muted-foreground"
                fill="none"
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          {/* Scrollable Content */}
          <div className="flex-1 overflow-y-auto p-6 space-y-6">
            {/* Progression type selection */}
            <div>
              <label className="block text-sm font-semibold mb-3 text-foreground">Progression Type</label>
              <div className="grid grid-cols-3 gap-3">
                {(['Linear', 'RepsPerSet', 'MinimalSets'] as ProgressionType[]).map((type) => (
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
                    {type === 'Linear' ? 'Hypertrophy' : type === 'RepsPerSet' ? 'Reps/Set' : 'Minimal Sets'}
                  </button>
                ))}
              </div>
              <div className="mt-3 p-3 bg-muted/30 rounded-xl border border-border/50">
                <p className="text-xs text-muted-foreground">
                  {progressionDescriptions[progressionType]}
                </p>
              </div>
            </div>

            {/* Progression-specific configuration */}
            <ExerciseConfigFields
              progressionType={progressionType}
              trainingMaxValue={trainingMaxValue}
              setTrainingMaxValue={setTrainingMaxValue}
              weightUnit={weightUnit}
              setWeightUnit={setWeightUnit}
              isPrimary={isPrimary}
              setIsPrimary={setIsPrimary}
              isUnilateral={isUnilateral}
              setIsUnilateral={setIsUnilateral}
              repRangeMin={repRangeMin}
              setRepRangeMin={setRepRangeMin}
              repRangeMax={repRangeMax}
              setRepRangeMax={setRepRangeMax}
              currentSets={currentSets}
              setCurrentSets={setCurrentSets}
              targetSets={targetSets}
              setTargetSets={setTargetSets}
              targetTotalReps={targetTotalReps}
              setTargetTotalReps={setTargetTotalReps}
              minSets={minSets}
              setMinSets={setMinSets}
              maxSets={maxSets}
              setMaxSets={setMaxSets}
              minimalCurrentSets={minimalCurrentSets}
              setMinimalCurrentSets={setMinimalCurrentSets}
            />

            {/* Day assignment */}
            <div>
              <label className="block text-sm font-semibold mb-3 text-foreground">Assign to Day</label>
              <div className="grid grid-cols-3 gap-3">
                {availableDays.map((day) => (
                  <button
                    key={day}
                    type="button"
                    onClick={() => setAssignedDay(day)}
                    className={`px-4 py-3 text-sm font-medium rounded-xl border-2 transition-all ${
                      assignedDay === day
                        ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
                        : "border-border hover:border-primary/50 hover:bg-muted/50 text-foreground"
                    }`}
                  >
                    Day {day}
                  </button>
                ))}
              </div>
              <div className="mt-3 p-3 bg-muted/30 rounded-xl border border-border/50">
                <p className="text-xs text-muted-foreground">
                  This exercise will be performed on Day {assignedDay} of your training week
                </p>
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="flex items-center justify-end gap-3 p-6 border-t border-border">
            <button
              type="button"
              onClick={onClose}
              className="px-5 py-2.5 text-sm font-medium rounded-xl border border-border hover:bg-muted transition-colors text-foreground"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="px-5 py-2.5 text-sm font-medium rounded-xl bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
            >
              Save Changes
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
