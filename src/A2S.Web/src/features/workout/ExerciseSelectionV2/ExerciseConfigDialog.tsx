import { useState, useEffect } from "react";
import type { SelectedExercise, DayNumber, ProgramVariant, RepRange, TrainingMax, WeightUnit, A2SProgressionType } from "@/types/workout";
import { A2SProgressionType as ProgressionTypeEnum, WeightUnit as WeightUnitEnum, ExerciseCategory } from "@/types/workout";

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
 */
export function ExerciseConfigDialog({
  exercise,
  isOpen,
  onClose,
  onSave,
  programVariant,
}: ExerciseConfigDialogProps) {
  const [progressionType, setProgressionType] = useState<A2SProgressionType>(ProgressionTypeEnum.RepsPerSet);
  const [assignedDay, setAssignedDay] = useState<DayNumber>(1);

  // Hypertrophy progression state
  const [trainingMaxValue, setTrainingMaxValue] = useState<number>(100);
  const [weightUnit, setWeightUnit] = useState<WeightUnit>(WeightUnitEnum.Kilograms);
  const [isPrimary, setIsPrimary] = useState<boolean>(true);
  const [baseSets, setBaseSets] = useState<number>(3);

  // RepsPerSet progression state
  const [repRangeMin, setRepRangeMin] = useState<number>(8);
  const [repRangeTarget, setRepRangeTarget] = useState<number>(10);
  const [repRangeMax, setRepRangeMax] = useState<number>(12);
  const [currentSets, setCurrentSets] = useState<number>(3);
  const [targetSets, setTargetSets] = useState<number>(5);
  const [startingWeight, setStartingWeight] = useState<number>(50);

  // Update form when exercise changes
  useEffect(() => {
    if (exercise) {
      setProgressionType(exercise.progressionType);
      setAssignedDay(exercise.assignedDay);

      // Hypertrophy progression
      if (exercise.trainingMax) {
        setTrainingMaxValue(exercise.trainingMax.value);
        setWeightUnit(exercise.trainingMax.unit);
      }
      if (exercise.isPrimary !== undefined) setIsPrimary(exercise.isPrimary);
      if (exercise.baseSetsPerExercise) setBaseSets(exercise.baseSetsPerExercise);

      // RepsPerSet progression
      if (exercise.repRange) {
        setRepRangeMin(exercise.repRange.minimum);
        setRepRangeTarget(exercise.repRange.target);
        setRepRangeMax(exercise.repRange.maximum);
      }
      if (exercise.currentSets) setCurrentSets(exercise.currentSets);
      if (exercise.targetSets) setTargetSets(exercise.targetSets);
      if (exercise.startingWeight) setStartingWeight(exercise.startingWeight);
      if (exercise.weightUnit !== undefined) setWeightUnit(exercise.weightUnit);
    }
  }, [exercise]);

  if (!isOpen || !exercise) return null;

  const handleSave = () => {
    const progressionUpdates = progressionType === ProgressionTypeEnum.Hypertrophy
      ? {
          progressionType,
          assignedDay,
          category: ExerciseCategory.MainLift, // For backend compatibility
          trainingMax: { value: trainingMaxValue, unit: weightUnit },
          isPrimary,
          baseSetsPerExercise: baseSets,
          // Clear RepsPerSet fields
          repRange: undefined,
          currentSets: undefined,
          targetSets: undefined,
          startingWeight: undefined,
        }
      : {
          progressionType,
          assignedDay,
          category: ExerciseCategory.Accessory, // For backend compatibility
          repRange: {
            minimum: repRangeMin,
            target: repRangeTarget,
            maximum: repRangeMax,
          },
          currentSets,
          targetSets,
          startingWeight,
          weightUnit,
          // Clear Hypertrophy fields
          trainingMax: undefined,
          isPrimary: undefined,
          baseSetsPerExercise: undefined,
        };

    onSave(exercise.id, progressionUpdates);
    onClose();
  };

  const availableDays = getAvailableDays(programVariant);

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/50 z-40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Dialog */}
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div
          className="bg-white dark:bg-gray-900 rounded-lg shadow-xl w-full max-w-2xl flex flex-col max-h-[90vh]"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 dark:border-gray-700">
            <div>
              <h2 className="text-xl font-bold text-gray-900 dark:text-gray-100">Configure Exercise</h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">{exercise.template.name}</p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="p-2 rounded-md hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
              aria-label="Close dialog"
            >
              <svg
                className="w-6 h-6 text-gray-500 dark:text-gray-400"
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
              <label className="block text-sm font-semibold mb-3 text-gray-900 dark:text-gray-100">Progression Type</label>
              <div className="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => setProgressionType(ProgressionTypeEnum.Hypertrophy)}
                  className={`px-4 py-3 text-sm font-medium rounded-md border-2 transition-all ${
                    progressionType === ProgressionTypeEnum.Hypertrophy
                      ? "bg-blue-50 dark:bg-blue-900/20 border-blue-500 text-blue-700 dark:text-blue-400"
                      : "border-gray-300 dark:border-gray-600 hover:border-blue-400 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300"
                  }`}
                >
                  A2S Hypertrophy
                </button>
                <button
                  type="button"
                  onClick={() => setProgressionType(ProgressionTypeEnum.RepsPerSet)}
                  className={`px-4 py-3 text-sm font-medium rounded-md border-2 transition-all ${
                    progressionType === ProgressionTypeEnum.RepsPerSet
                      ? "bg-blue-50 dark:bg-blue-900/20 border-blue-500 text-blue-700 dark:text-blue-400"
                      : "border-gray-300 dark:border-gray-600 hover:border-blue-400 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300"
                  }`}
                >
                  A2S Reps Per Set
                </button>
              </div>
              <div className="mt-3 p-3 bg-gray-100 dark:bg-gray-800 rounded-md">
                <p className="text-xs text-gray-600 dark:text-gray-400">
                  {progressionType === ProgressionTypeEnum.Hypertrophy
                    ? "Uses training max percentages with AMRAP sets for strength and hypertrophy"
                    : "Progresses by adding sets and reps, then increasing weight"}
                </p>
              </div>
            </div>

            {/* Progression-specific configuration */}
            {progressionType === ProgressionTypeEnum.Hypertrophy ? (
              <div className="space-y-5 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
                <h3 className="font-semibold text-sm text-gray-900 dark:text-gray-100">A2S Hypertrophy Settings</h3>

                {/* Training Max */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Training Max</label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      value={trainingMaxValue}
                      onChange={(e) => setTrainingMaxValue(Number(e.target.value))}
                      className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                      min="0"
                      step="2.5"
                    />
                    <select
                      value={weightUnit}
                      onChange={(e) => setWeightUnit(Number(e.target.value) as WeightUnit)}
                      className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                    >
                      <option value={WeightUnitEnum.Kilograms}>kg</option>
                      <option value={WeightUnitEnum.Pounds}>lbs</option>
                    </select>
                  </div>
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Your training max should be ~90-95% of your 1RM
                  </p>
                </div>

                {/* Primary vs Auxiliary */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Lift Type</label>
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      type="button"
                      onClick={() => setIsPrimary(true)}
                      className={`px-3 py-2 text-sm font-medium rounded-md border-2 transition-all ${
                        isPrimary
                          ? "bg-blue-50 dark:bg-blue-900/20 border-blue-500 text-blue-700 dark:text-blue-400"
                          : "border-gray-300 dark:border-gray-600 hover:border-blue-400 text-gray-700 dark:text-gray-300"
                      }`}
                    >
                      Primary
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsPrimary(false)}
                      className={`px-3 py-2 text-sm font-medium rounded-md border-2 transition-all ${
                        !isPrimary
                          ? "bg-blue-50 dark:bg-blue-900/20 border-blue-500 text-blue-700 dark:text-blue-400"
                          : "border-gray-300 dark:border-gray-600 hover:border-blue-400 text-gray-700 dark:text-gray-300"
                      }`}
                    >
                      Auxiliary
                    </button>
                  </div>
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Both use AMRAP sets with different weight/rep calculations
                  </p>
                </div>

                {/* Number of Sets */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Number of Sets</label>
                  <input
                    type="number"
                    value={baseSets}
                    onChange={(e) => setBaseSets(Number(e.target.value))}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                    min="1"
                    max="10"
                  />
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Typical range: 3-5 sets per exercise
                  </p>
                </div>
              </div>
            ) : (
              <div className="space-y-5 p-4 bg-gray-50 dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
                <h3 className="font-semibold text-sm text-gray-900 dark:text-gray-100">A2S Reps Per Set Settings</h3>

                {/* Rep Range */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Rep Range</label>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="text-xs text-gray-600 dark:text-gray-400">Min</label>
                      <input
                        type="number"
                        value={repRangeMin}
                        onChange={(e) => setRepRangeMin(Number(e.target.value))}
                        className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                        min="1"
                        max="30"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-600 dark:text-gray-400">Target</label>
                      <input
                        type="number"
                        value={repRangeTarget}
                        onChange={(e) => setRepRangeTarget(Number(e.target.value))}
                        className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                        min="1"
                        max="30"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-600 dark:text-gray-400">Max</label>
                      <input
                        type="number"
                        value={repRangeMax}
                        onChange={(e) => setRepRangeMax(Number(e.target.value))}
                        className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-100 dark:text-gray-100"
                        min="1"
                        max="30"
                      />
                    </div>
                  </div>
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Common ranges: 6-8, 8-10, 10-12, 12-15 reps
                  </p>
                </div>

                {/* Starting Weight */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Starting Weight</label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      value={startingWeight}
                      onChange={(e) => setStartingWeight(Number(e.target.value))}
                      className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                      min="0"
                      step="2.5"
                    />
                    <select
                      value={weightUnit}
                      onChange={(e) => setWeightUnit(Number(e.target.value) as WeightUnit)}
                      className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                    >
                      <option value={WeightUnitEnum.Kilograms}>kg</option>
                      <option value={WeightUnitEnum.Pounds}>lbs</option>
                    </select>
                  </div>
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Choose a weight you can perform comfortably in the target rep range
                  </p>
                </div>

                {/* Set Progression */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-gray-900 dark:text-gray-100">Set Progression</label>
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-xs text-gray-600 dark:text-gray-400">Current Sets</label>
                      <input
                        type="number"
                        value={currentSets}
                        onChange={(e) => setCurrentSets(Number(e.target.value))}
                        className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                        min="1"
                        max="10"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-600 dark:text-gray-400">Target Sets</label>
                      <input
                        type="number"
                        value={targetSets}
                        onChange={(e) => setTargetSets(Number(e.target.value))}
                        className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100"
                        min="1"
                        max="10"
                      />
                    </div>
                  </div>
                  <p className="text-xs text-gray-600 dark:text-gray-400 mt-2">
                    Progress from current sets to target sets before increasing weight
                  </p>
                </div>
              </div>
            )}

            {/* Day assignment */}
            <div>
              <label className="block text-sm font-semibold mb-3 text-gray-900 dark:text-gray-100">Assign to Day</label>
              <div className="grid grid-cols-3 gap-3">
                {availableDays.map((day) => (
                  <button
                    key={day}
                    type="button"
                    onClick={() => setAssignedDay(day)}
                    className={`px-4 py-3 text-sm font-medium rounded-md border-2 transition-all ${
                      assignedDay === day
                        ? "bg-blue-50 dark:bg-blue-900/20 border-blue-500 text-blue-700 dark:text-blue-400"
                        : "border-gray-300 dark:border-gray-600 hover:border-blue-400 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300"
                    }`}
                  >
                    Day {day}
                  </button>
                ))}
              </div>
              <div className="mt-3 p-3 bg-gray-100 dark:bg-gray-800 rounded-md">
                <p className="text-xs text-gray-600 dark:text-gray-400">
                  This exercise will be performed on Day {assignedDay} of your training week
                </p>
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="flex items-center justify-end gap-3 p-6 border-t border-gray-200 dark:border-gray-700">
            <button
              type="button"
              onClick={onClose}
              className="px-5 py-2.5 text-sm font-medium rounded-md border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors text-gray-700 dark:text-gray-300"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="px-5 py-2.5 text-sm font-medium rounded-md bg-blue-600 text-white hover:bg-blue-700 transition-colors"
            >
              Save Changes
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
