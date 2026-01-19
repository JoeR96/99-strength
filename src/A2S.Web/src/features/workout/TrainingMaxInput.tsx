import { WeightUnit } from "@/types/workout";
import type { TrainingMax } from "@/types/workout";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";

interface TrainingMaxValues {
  squat: TrainingMax;
  bench: TrainingMax;
  deadlift: TrainingMax;
  overheadPress: TrainingMax;
}

interface TrainingMaxInputProps {
  trainingMaxes: TrainingMaxValues;
  onUpdate: (values: TrainingMaxValues) => void;
}

export function TrainingMaxInput({ trainingMaxes, onUpdate }: TrainingMaxInputProps) {
  const updateMax = (exercise: keyof TrainingMaxValues, value: number) => {
    onUpdate({
      ...trainingMaxes,
      [exercise]: {
        ...trainingMaxes[exercise],
        value,
      },
    });
  };

  const updateUnit = (unit: WeightUnit) => {
    onUpdate({
      squat: { ...trainingMaxes.squat, unit },
      bench: { ...trainingMaxes.bench, unit },
      deadlift: { ...trainingMaxes.deadlift, unit },
      overheadPress: { ...trainingMaxes.overheadPress, unit },
    });
  };

  const unit = trainingMaxes.squat.unit; // All should have same unit

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold mb-2">Set Your Training Maxes</h2>
        <p className="text-muted-foreground">
          Enter your current training max for each main lift. Your training max should be approximately 90-95% of your 1RM.
        </p>
      </div>

      <div className="space-y-4">
        {/* Unit selector */}
        <div>
          <Label>Weight Unit</Label>
          <div className="mt-2 flex gap-4">
            <button
              type="button"
              onClick={() => updateUnit(WeightUnit.Kilograms)}
              className={`px-4 py-2 rounded-md border ${
                unit === WeightUnit.Kilograms
                  ? "bg-primary text-primary-foreground"
                  : "bg-background"
              }`}
            >
              Kilograms
            </button>
            <button
              type="button"
              onClick={() => updateUnit(WeightUnit.Pounds)}
              className={`px-4 py-2 rounded-md border ${
                unit === WeightUnit.Pounds
                  ? "bg-primary text-primary-foreground"
                  : "bg-background"
              }`}
            >
              Pounds
            </button>
          </div>
        </div>

        {/* Training Max inputs */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <Label htmlFor="squat">Squat</Label>
            <div className="mt-1 flex items-center gap-2">
              <Input
                id="squat"
                type="number"
                value={trainingMaxes.squat.value}
                onChange={(e) => updateMax("squat", Number(e.target.value))}
                min="0"
                step="2.5"
                className="flex-1"
              />
              <span className="text-sm text-muted-foreground">
                {unit === WeightUnit.Kilograms ? "kg" : "lbs"}
              </span>
            </div>
          </div>

          <div>
            <Label htmlFor="bench">Bench Press</Label>
            <div className="mt-1 flex items-center gap-2">
              <Input
                id="bench"
                type="number"
                value={trainingMaxes.bench.value}
                onChange={(e) => updateMax("bench", Number(e.target.value))}
                min="0"
                step="2.5"
                className="flex-1"
              />
              <span className="text-sm text-muted-foreground">
                {unit === WeightUnit.Kilograms ? "kg" : "lbs"}
              </span>
            </div>
          </div>

          <div>
            <Label htmlFor="deadlift">Deadlift</Label>
            <div className="mt-1 flex items-center gap-2">
              <Input
                id="deadlift"
                type="number"
                value={trainingMaxes.deadlift.value}
                onChange={(e) => updateMax("deadlift", Number(e.target.value))}
                min="0"
                step="2.5"
                className="flex-1"
              />
              <span className="text-sm text-muted-foreground">
                {unit === WeightUnit.Kilograms ? "kg" : "lbs"}
              </span>
            </div>
          </div>

          <div>
            <Label htmlFor="overheadPress">Overhead Press</Label>
            <div className="mt-1 flex items-center gap-2">
              <Input
                id="overheadPress"
                type="number"
                value={trainingMaxes.overheadPress.value}
                onChange={(e) => updateMax("overheadPress", Number(e.target.value))}
                min="0"
                step="2.5"
                className="flex-1"
              />
              <span className="text-sm text-muted-foreground">
                {unit === WeightUnit.Kilograms ? "kg" : "lbs"}
              </span>
            </div>
          </div>
        </div>

        <div className="mt-4 p-4 bg-muted rounded-md">
          <h4 className="font-semibold text-sm mb-2">What is a Training Max?</h4>
          <p className="text-sm text-muted-foreground">
            Your Training Max (TM) is approximately 90-95% of your true 1-rep max. It's used to calculate
            your working weights for each workout. As you progress through the program, your TM will
            automatically adjust based on your performance.
          </p>
        </div>
      </div>
    </div>
  );
}
