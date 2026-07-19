import type { WeightUnit } from "@/types/workout";
import { WeightUnit as WeightUnitEnum } from "@/types/workout";
import { UnilateralToggle } from "@/components/shared/UnilateralToggle";

// Extended type to include MinimalSets
type ProgressionType = 'Linear' | 'RepsPerSet' | 'MinimalSets';

interface ExerciseConfigFieldsProps {
  progressionType: ProgressionType;

  // Linear (Hypertrophy) progression state
  trainingMaxValue: number;
  setTrainingMaxValue: (value: number) => void;
  weightUnit: WeightUnit;
  setWeightUnit: (value: WeightUnit) => void;
  isPrimary: boolean;
  setIsPrimary: (value: boolean) => void;

  // RepsPerSet progression state
  isUnilateral: boolean;
  setIsUnilateral: (value: boolean) => void;
  repRangeMin: number;
  setRepRangeMin: (value: number) => void;
  repRangeMax: number;
  setRepRangeMax: (value: number) => void;
  currentSets: number;
  setCurrentSets: (value: number) => void;
  targetSets: number;
  setTargetSets: (value: number) => void;

  // MinimalSets progression state
  targetTotalReps: number;
  setTargetTotalReps: (value: number) => void;
  minSets: number;
  setMinSets: (value: number) => void;
  maxSets: number;
  setMaxSets: (value: number) => void;
  minimalCurrentSets: number;
  setMinimalCurrentSets: (value: number) => void;
}

/**
 * Progression-specific configuration fields for ExerciseConfigDialog.
 * Renders the Linear (Hypertrophy), RepsPerSet, or MinimalSets field group
 * depending on the selected progression type.
 */
export function ExerciseConfigFields({
  progressionType,
  trainingMaxValue,
  setTrainingMaxValue,
  weightUnit,
  setWeightUnit,
  isPrimary,
  setIsPrimary,
  isUnilateral,
  setIsUnilateral,
  repRangeMin,
  setRepRangeMin,
  repRangeMax,
  setRepRangeMax,
  currentSets,
  setCurrentSets,
  targetSets,
  setTargetSets,
  targetTotalReps,
  setTargetTotalReps,
  minSets,
  setMinSets,
  maxSets,
  setMaxSets,
  minimalCurrentSets,
  setMinimalCurrentSets,
}: ExerciseConfigFieldsProps) {
  return (
    <>
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
                    ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
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
                    ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
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
            <div className="grid grid-cols-2 gap-2">
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

          {/* Starting Weight Info */}
          <div className="p-3 bg-muted/50 rounded-xl border border-border">
            <p className="text-sm text-muted-foreground">
              Weight will be set after your first session. Just enter the weight you use during your workout and confirm it afterwards.
            </p>
          </div>

          {/* Set Progression */}
          <div>
            <label className="block text-sm font-medium mb-2 text-foreground">Set Progression</label>
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="text-xs text-muted-foreground">Starting Sets</label>
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
              Progress from starting sets to target sets before increasing weight
            </p>
          </div>

          {/* Unilateral Toggle */}
          <UnilateralToggle
            isUnilateral={isUnilateral}
            onChange={setIsUnilateral}
          />
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

          {/* Weight info (set after first session) */}
          <div className="p-3 bg-muted/50 rounded-xl border border-border">
            <p className="text-sm text-muted-foreground">
              Weight / assistance will be set after your first session. Just enter the load you use during your workout and confirm it afterwards.
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
                <label className="text-xs text-muted-foreground">Starting Sets</label>
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
    </>
  );
}
