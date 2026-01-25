import type { CreateExerciseRequest } from '@/types/workout';
import { WeightUnit, ExerciseCategory } from '@/types/workout';

/**
 * Workout template definition
 */
export interface WorkoutTemplate {
  id: string;
  name: string;
  description: string;
  variant: 4 | 5 | 6;
  totalWeeks: number;
  exercises: CreateExerciseRequest[];
}

/**
 * 4-Day Hypertrophy Template
 * Based on the A2S 2024-2025 program spreadsheet
 * - Linear progression for main lifts (Overhead Press, Smith Squat, Front Squat)
 * - RepsPerSet progression for accessories
 */
const fourDayHypertrophyTemplate: WorkoutTemplate = {
  id: 'four-day-hypertrophy',
  name: '4-Day Hypertrophy',
  description: 'A balanced 4-day split focusing on hypertrophy with 3 main lifts and targeted accessories.',
  variant: 4,
  totalWeeks: 21,
  exercises: [
    // ==================== DAY 1 ====================
    // Lat Pulldown - RepsPerSet (3 sets x 12 reps -> 5 sets)
    {
      templateName: "Lat Pulldown",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 1,
      startingWeight: 50,
      weightUnit: WeightUnit.Kilograms,
    },
    // Overhead Press (Smith Machine) - Linear (TM: 65kg)
    {
      templateName: "Overhead Press Smith Machine",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 1,
      orderInDay: 2,
      trainingMaxValue: 65,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Cable Low Row - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Cable Low Row",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 3,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Cable Lateral Raise - RepsPerSet (4 sets x 8 reps)
    {
      templateName: "Cable Lateral Raise",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 4,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
    },
    // Cable Bicep Curl - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Cable Bicep Curl",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 5,
      startingWeight: 15,
      weightUnit: WeightUnit.Kilograms,
    },
    // Cable Tricep Pushdown - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Cable Tricep Pushdown",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 6,
      startingWeight: 20,
      weightUnit: WeightUnit.Kilograms,
    },
    // Rear Delt Flyes - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Rear Delt Flyes",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 7,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 2 ====================
    // Smith Squat - Linear (TM: 107.5kg)
    {
      templateName: "Smith Squat",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 2,
      orderInDay: 1,
      trainingMaxValue: 107.5,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Single Leg Lunge - RepsPerSet (4 sets x 9 reps)
    {
      templateName: "Single Leg Lunge Smith Machine",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 2,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Lying Leg Curl - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Lying Leg Curl",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 3,
      startingWeight: 30,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Abduction - RepsPerSet (3 sets x 12 reps)
    {
      templateName: "Hip Abduction",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 4,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Calf Raises - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "Calf Raises",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 5,
      startingWeight: 60,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 3 ====================
    // Assisted Dips - RepsPerSet (using as proxy for MinimalSets, 3 sets, 40 total reps)
    {
      templateName: "Assisted Dips",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 1,
      startingWeight: 32,
      weightUnit: WeightUnit.Kilograms,
    },
    // Assisted Pullups - RepsPerSet (using as proxy for MinimalSets, 6 sets, 40 total reps)
    {
      templateName: "Assisted Pullups",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 2,
      startingWeight: 32,
      weightUnit: WeightUnit.Kilograms,
    },
    // Concentration Curl - RepsPerSet (4 sets x 15 reps)
    {
      templateName: "Concentration Curl",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 3,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
    },
    // Ez Curl - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "Ez Curl",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 4,
      startingWeight: 20,
      weightUnit: WeightUnit.Kilograms,
    },
    // Single Arm Tricep Pushdown - RepsPerSet (6 sets x 25 reps)
    {
      templateName: "Single Arm Tricep Pushdown",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 5,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
    },
    // Lateral Raises - RepsPerSet (3 sets x 20 reps)
    {
      templateName: "Lateral Raises",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 6,
      startingWeight: 8,
      weightUnit: WeightUnit.Kilograms,
    },
    // Chest Flye - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Chest Flye",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 7,
      startingWeight: 15,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 4 ====================
    // Booty Builder - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Booty Builder",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 1,
      startingWeight: 60,
      weightUnit: WeightUnit.Kilograms,
    },
    // Front Squat - Linear (TM: 80kg)
    {
      templateName: "Front Squat",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 4,
      orderInDay: 2,
      trainingMaxValue: 80,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Single Leg Press - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Single Leg Press",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 3,
      startingWeight: 80,
      weightUnit: WeightUnit.Kilograms,
    },
    // Leg Extension - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Leg Extension",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 4,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Adduction - RepsPerSet (4 sets)
    {
      templateName: "Hip Adduction",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 5,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
  ] as CreateExerciseRequest[],
};

/**
 * All available workout templates
 */
export const workoutTemplates: WorkoutTemplate[] = [
  fourDayHypertrophyTemplate,
];

/**
 * Get a template by ID
 */
export function getTemplateById(id: string): WorkoutTemplate | undefined {
  return workoutTemplates.find(t => t.id === id);
}
