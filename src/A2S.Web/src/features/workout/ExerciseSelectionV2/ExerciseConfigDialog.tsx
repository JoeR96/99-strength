import { useState, useEffect } from "react";
import type { SelectedExercise, DayNumber, ProgramVariant, WeightUnit } from "@/types/workout";
import { A2SProgressionType as ProgressionTypeEnum, WeightUnit as WeightUnitEnum, ExerciseCategory } from "@/types/workout";

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
  const [baseSets, setBaseSets] = useState<number>(3);

  // RepsPerSet progression state
  const [repRangeMin, setRepRangeMin] = useState<number>(8);
  const [repRangeTarget, setRepRangeTarget] = useState<number>(10);
  const [repRangeMax, setRepRangeMax] = useState<number>(12);
  const [currentSets, setCurrentSets] = useState<number>(3);
  const [targetSets, setTargetSets] = useState<number>(5);
  const [startingWeight, setStartingWeight] = useState<number>(50);

  // MinimalSets progression state
  const [minimalSetsWeight, setMinimalSetsWeight] = useState<number>(0);
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
    let progressionUpdates;

    if (progressionType === ProgressionTypeEnum.Linear) {
      progressionUpdates = {
        progressionType: progressionType as 'Linear',
        assignedDay,
        category: ExerciseCategory.MainLift,
        trainingMax: { value: trainingMaxValue, unit: weightUnit },
        isPrimary,
        baseSetsPerExercise: baseSets,
        repRange: undefined,
        currentSets: undefined,
        targetSets: undefined,
        startingWeight: undefined,
      };
    } else if (progressionType === 'MinimalSets') {
      // MinimalSets maps to RepsPerSet on the backend for now
      progressionUpdates = {
        progressionType: 'RepsPerSet' as const,
        assignedDay,
        category: ExerciseCategory.Accessory,
        startingWeight: minimalSetsWeight,
        weightUnit,
        currentSets: minimalCurrentSets,
        targetSets: maxSets,
        repRange: {
          minimum: Math.floor(targetTotalReps / maxSets),
          target: Math.floor(targetTotalReps / minimalCurrentSets),
          maximum: Math.ceil(targetTotalReps / minSets),
        },
        trainingMax: undefined,
        isPrimary: undefined,
        baseSetsPerExercise: undefined,
      };
    } else {
      progressionUpdates = {
        progressionType: progressionType as 'RepsPerSet',
        assignedDay,
        category: ExerciseCategory.Accessory,
        repRange: {
          minimum: repRangeMin,
          target: repRangeTarget,
          maximum: repRangeMax,
        },
        currentSets,
        targetSets,
        startingWeight,
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
                        ? "bg-primary/10 border-primary text-primary"
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
            {progressionType === 'Linear' && (
              <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
                <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
                  <svg className="w-4 h-4 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                  </svg>
                  Hypertrophy Settings
                </h3>

                {/* Training Max */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Training Max</label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      value={trainingMaxValue}
                      onChange={(e) => setTrainingMaxValue(Number(e.target.value))}
                      className="flex-1 px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                      min="0"
                      step="2.5"
                    />
                    <select
                      value={weightUnit}
                      onChange={(e) => setWeightUnit(Number(e.target.value) as WeightUnit)}
                      className="px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                    >
                      <option value={WeightUnitEnum.Kilograms}>kg</option>
                      <option value={WeightUnitEnum.Pounds}>lbs</option>
                    </select>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Your training max should be ~90-95% of your 1RM
                  </p>
                </div>

                {/* Primary vs Auxiliary */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Lift Type</label>
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      type="button"
                      onClick={() => setIsPrimary(true)}
                      className={`px-3 py-2.5 text-sm font-medium rounded-xl border-2 transition-all ${
                        isPrimary
                          ? "bg-primary/10 border-primary text-primary"
                          : "border-border hover:border-primary/50 text-foreground"
                      }`}
                    >
                      Primary
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsPrimary(false)}
                      className={`px-3 py-2.5 text-sm font-medium rounded-xl border-2 transition-all ${
                        !isPrimary
                          ? "bg-primary/10 border-primary text-primary"
                          : "border-border hover:border-primary/50 text-foreground"
                      }`}
                    >
                      Auxiliary
                    </button>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Primary lifts use heavier weights with lower reps
                  </p>
                </div>

                {/* Number of Sets */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Number of Sets</label>
                  <input
                    type="number"
                    value={baseSets}
                    onChange={(e) => setBaseSets(Number(e.target.value))}
                    className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                    min="1"
                    max="10"
                  />
                  <p className="text-xs text-muted-foreground mt-2">
                    Typical range: 3-5 sets per exercise
                  </p>
                </div>
              </div>
            )}

            {progressionType === 'RepsPerSet' && (
              <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
                <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
                  <svg className="w-4 h-4 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                  </svg>
                  Reps Per Set Settings
                </h3>

                {/* Rep Range */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Rep Range</label>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="text-xs text-muted-foreground">Min</label>
                      <input
                        type="number"
                        value={repRangeMin}
                        onChange={(e) => setRepRangeMin(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="30"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-muted-foreground">Target</label>
                      <input
                        type="number"
                        value={repRangeTarget}
                        onChange={(e) => setRepRangeTarget(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="30"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-muted-foreground">Max</label>
                      <input
                        type="number"
                        value={repRangeMax}
                        onChange={(e) => setRepRangeMax(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="30"
                      />
                    </div>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Common ranges: 6-8, 8-10, 10-12, 12-15 reps
                  </p>
                </div>

                {/* Starting Weight */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Starting Weight</label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      value={startingWeight}
                      onChange={(e) => setStartingWeight(Number(e.target.value))}
                      className="flex-1 px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                      min="0"
                      step="2.5"
                    />
                    <select
                      value={weightUnit}
                      onChange={(e) => setWeightUnit(Number(e.target.value) as WeightUnit)}
                      className="px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                    >
                      <option value={WeightUnitEnum.Kilograms}>kg</option>
                      <option value={WeightUnitEnum.Pounds}>lbs</option>
                    </select>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Choose a weight you can perform comfortably in the target rep range
                  </p>
                </div>

                {/* Set Progression */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Set Progression</label>
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-xs text-muted-foreground">Current Sets</label>
                      <input
                        type="number"
                        value={currentSets}
                        onChange={(e) => setCurrentSets(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="10"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-muted-foreground">Target Sets</label>
                      <input
                        type="number"
                        value={targetSets}
                        onChange={(e) => setTargetSets(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="10"
                      />
                    </div>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Progress from current sets to target sets before increasing weight
                  </p>
                </div>
              </div>
            )}

            {progressionType === 'MinimalSets' && (
              <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
                <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
                  <svg className="w-4 h-4 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                  </svg>
                  Minimal Sets Settings
                </h3>

                {/* Weight (for assisted exercises, can be 0 or negative for assistance) */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Weight / Assistance</label>
                  <div className="flex gap-2">
                    <input
                      type="number"
                      value={minimalSetsWeight}
                      onChange={(e) => setMinimalSetsWeight(Number(e.target.value))}
                      className="flex-1 px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                      step="2.5"
                    />
                    <select
                      value={weightUnit}
                      onChange={(e) => setWeightUnit(Number(e.target.value) as WeightUnit)}
                      className="px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                    >
                      <option value={WeightUnitEnum.Kilograms}>kg</option>
                      <option value={WeightUnitEnum.Pounds}>lbs</option>
                    </select>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Use 0 for bodyweight, negative for assisted (e.g., -30 for band assistance)
                  </p>
                </div>

                {/* Target Total Reps */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Target Total Reps</label>
                  <input
                    type="number"
                    value={targetTotalReps}
                    onChange={(e) => setTargetTotalReps(Number(e.target.value))}
                    className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                    min="10"
                    max="100"
                  />
                  <p className="text-xs text-muted-foreground mt-2">
                    Total reps to achieve across all sets (typically 30-50)
                  </p>
                </div>

                {/* Set Range */}
                <div>
                  <label className="block text-sm font-medium mb-2 text-foreground">Set Range</label>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="text-xs text-muted-foreground">Min Sets</label>
                      <input
                        type="number"
                        value={minSets}
                        onChange={(e) => setMinSets(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="10"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-muted-foreground">Current Sets</label>
                      <input
                        type="number"
                        value={minimalCurrentSets}
                        onChange={(e) => setMinimalCurrentSets(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="10"
                      />
                    </div>
                    <div>
                      <label className="text-xs text-muted-foreground">Max Sets</label>
                      <input
                        type="number"
                        value={maxSets}
                        onChange={(e) => setMaxSets(Number(e.target.value))}
                        className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
                        min="1"
                        max="10"
                      />
                    </div>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Goal: hit total reps in fewer sets over time
                  </p>
                </div>
              </div>
            )}

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
                        ? "bg-primary/10 border-primary text-primary"
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
