import { EquipmentType, WeightUnit } from "@/types/workout";

// Unified progression configuration
export type ProgressionConfig =
  | LinearConfig
  | RepsPerSetConfig
  | MinimalSetsConfig;

export interface LinearConfig {
  type: "Linear";
  trainingMaxValue: number;
  trainingMaxUnit: WeightUnit;
  baseSets: number;
  isPrimary: boolean;
}

export interface RepsPerSetConfig {
  type: "RepsPerSet";
  startingWeight: number;
  weightUnit: WeightUnit;
  repRangeMin: number;
  repRangeMax: number;
  currentSets: number;
  targetSets: number;
}

export interface MinimalSetsConfig {
  type: "MinimalSets";
  weight: number;
  weightUnit: WeightUnit;
  targetTotalReps: number;
  minSets: number;
  currentSets: number;
  maxSets: number;
}

export type ProgressionType = "Linear" | "RepsPerSet" | "MinimalSets";

// Equipment types that work well with A2S Linear/AMRAP progression
export const AMRAP_COMPATIBLE_EQUIPMENT: readonly EquipmentType[] = [
  EquipmentType.Barbell,
  EquipmentType.SmithMachine,
];

export function getEquipmentLabel(equipment: EquipmentType): string {
  switch (equipment) {
    case EquipmentType.Barbell:
      return "Barbell";
    case EquipmentType.Dumbbell:
      return "Dumbbell";
    case EquipmentType.Cable:
      return "Cable";
    case EquipmentType.Machine:
      return "Machine";
    case EquipmentType.Bodyweight:
      return "Bodyweight";
    case EquipmentType.SmithMachine:
      return "Smith Machine";
    default:
      return "Unknown";
  }
}

export function LinearSettingsForm({
  trainingMaxValue,
  setTrainingMaxValue,
  weightUnit,
  setWeightUnit,
  isPrimary,
  setIsPrimary,
}: {
  trainingMaxValue: number;
  setTrainingMaxValue: (v: number) => void;
  weightUnit: WeightUnit;
  setWeightUnit: (v: WeightUnit) => void;
  isPrimary: boolean;
  setIsPrimary: (v: boolean) => void;
}) {
  return (
    <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
      <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
        <svg
          className="w-4 h-4 text-primary"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"
          />
        </svg>
        Hypertrophy Settings
      </h3>

      {/* Training Max */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Training Max
        </label>
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
            <option value={WeightUnit.Kilograms}>kg</option>
            <option value={WeightUnit.Pounds}>lbs</option>
          </select>
        </div>
        <p className="text-xs text-muted-foreground mt-2">
          Your training max should be ~90-95% of your 1RM
        </p>
      </div>

      {/* Primary vs Auxiliary */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Lift Type
        </label>
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
      </div>
    </div>
  );
}

export function RepsPerSetSettingsForm({
  repRangeMin,
  setRepRangeMin,
  repRangeMax,
  setRepRangeMax,
  startingWeight,
  setStartingWeight,
  weightUnit,
  setWeightUnit,
  currentSets,
  setCurrentSets,
  targetSets,
  setTargetSets,
}: {
  repRangeMin: number;
  setRepRangeMin: (v: number) => void;
  repRangeMax: number;
  setRepRangeMax: (v: number) => void;
  startingWeight: number;
  setStartingWeight: (v: number) => void;
  weightUnit: WeightUnit;
  setWeightUnit: (v: WeightUnit) => void;
  currentSets: number;
  setCurrentSets: (v: number) => void;
  targetSets: number;
  setTargetSets: (v: number) => void;
}) {
  return (
    <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
      <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
        <svg
          className="w-4 h-4 text-primary"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"
          />
        </svg>
        Reps Per Set Settings
      </h3>

      {/* Rep Range */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Rep Range
        </label>
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
      </div>

      {/* Starting Weight */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Starting Weight
        </label>
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
            <option value={WeightUnit.Kilograms}>kg</option>
            <option value={WeightUnit.Pounds}>lbs</option>
          </select>
        </div>
      </div>

      {/* Set Progression */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Set Progression
        </label>
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
      </div>
    </div>
  );
}

export function MinimalSetsSettingsForm({
  minimalSetsWeight,
  setMinimalSetsWeight,
  weightUnit,
  setWeightUnit,
  targetTotalReps,
  setTargetTotalReps,
  minSets,
  setMinSets,
  minimalCurrentSets,
  setMinimalCurrentSets,
  maxSets,
  setMaxSets,
}: {
  minimalSetsWeight: number;
  setMinimalSetsWeight: (v: number) => void;
  weightUnit: WeightUnit;
  setWeightUnit: (v: WeightUnit) => void;
  targetTotalReps: number;
  setTargetTotalReps: (v: number) => void;
  minSets: number;
  setMinSets: (v: number) => void;
  minimalCurrentSets: number;
  setMinimalCurrentSets: (v: number) => void;
  maxSets: number;
  setMaxSets: (v: number) => void;
}) {
  return (
    <div className="space-y-5 p-5 bg-muted/20 rounded-xl border border-border/50">
      <h3 className="font-semibold text-sm text-foreground flex items-center gap-2">
        <svg
          className="w-4 h-4 text-primary"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
          />
        </svg>
        Minimal Sets Settings
      </h3>

      {/* Weight */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Weight / Assistance
        </label>
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
            <option value={WeightUnit.Kilograms}>kg</option>
            <option value={WeightUnit.Pounds}>lbs</option>
          </select>
        </div>
        <p className="text-xs text-muted-foreground mt-2">
          Use 0 for bodyweight, negative for assisted
        </p>
      </div>

      {/* Target Total Reps */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Target Total Reps
        </label>
        <input
          type="number"
          value={targetTotalReps}
          onChange={(e) => setTargetTotalReps(Number(e.target.value))}
          className="w-full px-3 py-2.5 border border-border rounded-xl focus:border-primary focus:ring-1 focus:ring-primary focus:outline-none bg-background text-foreground"
          min="10"
          max="100"
        />
      </div>

      {/* Set Range */}
      <div>
        <label className="block text-sm font-medium mb-2 text-foreground">
          Set Range
        </label>
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
      </div>
    </div>
  );
}
