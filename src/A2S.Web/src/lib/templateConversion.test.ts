import { describe, it, expect } from "vitest";
import { convertTemplateToSelectedExercises } from "./templateConversion";
import { WeightUnit, ExerciseCategory } from "@/types/workout";
import type { ExerciseLibrary, CreateExerciseRequest } from "@/types/workout";
import type { WorkoutTemplate } from "@/data/workoutTemplates";

const library: ExerciseLibrary = {
  templates: [
    { name: "Squat Barbell", equipment: 0, description: "", defaultSets: 4, defaultRepRange: { minimum: 8, maximum: 12 } },
  ],
};

const template: WorkoutTemplate = {
  id: "test-program",
  name: "Test Program",
  description: "",
  variant: 4,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  exercises: [
    {
      templateName: "Squat Barbell",
      externalTemplateId: "hevy-123",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 1,
      orderInDay: 1,
      trainingMaxValue: 105,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    {
      templateName: "Unknown Exercise",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 1,
      repRangeMinimum: 6,
      repRangeMaximum: 10,
      startingSets: 3,
      targetSets: 5,
      startingWeight: 45,
    },
  ] as CreateExerciseRequest[],
};

describe("convertTemplateToSelectedExercises", () => {
  it("returns [] when library is null", () => {
    expect(convertTemplateToSelectedExercises(template, null as unknown as ExerciseLibrary)).toEqual([]);
  });
  it("maps a known Linear main lift with training max and Primary flag", () => {
    const [ex] = convertTemplateToSelectedExercises(template, library);
    expect(ex.template.name).toBe("Squat Barbell");
    expect(ex.progressionType).toBe("Linear");
    expect(ex.trainingMax).toEqual({ value: 105, unit: WeightUnit.Kilograms });
    expect(ex.isPrimary).toBe(true);
    expect(ex.assignedDay).toBe(1);
    expect(ex.hevyExerciseTemplateId).toBe("hevy-123");
  });
  it("falls back to a stub template for unknown names and preserves rep range/sets", () => {
    const ex = convertTemplateToSelectedExercises(template, library)[1];
    expect(ex.template.name).toBe("Unknown Exercise");
    expect(ex.repRange).toEqual({ minimum: 6, maximum: 10 });
    expect(ex.currentSets).toBe(3);
    expect(ex.targetSets).toBe(5);
    expect(ex.startingWeight).toBe(45);
    expect(ex.isPrimary).toBe(false);
  });
});
