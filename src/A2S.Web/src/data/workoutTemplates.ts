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
 *
 * IMPORTANT: templateName must match exactly with ExerciseLibrary names
 * hevyExerciseTemplateId must match Hevy's exercise IDs from hevyExercises.ts
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
      templateName: "Lat Pulldown (Cable)",
      hevyExerciseTemplateId: "6A6C31A5",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 1,
      startingWeight: 50,
      weightUnit: WeightUnit.Kilograms,
    },
    // Overhead Press (Smith Machine) - Linear (TM: 65kg)
    {
      templateName: "Overhead Press (Smith Machine)",
      hevyExerciseTemplateId: "B09A1304",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 1,
      orderInDay: 2,
      trainingMaxValue: 65,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Seated Cable Row - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Seated Cable Row - V Grip (Cable)",
      hevyExerciseTemplateId: "0393F233",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 3,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Lateral Raise (Cable) - RepsPerSet (4 sets x 8 reps)
    {
      templateName: "Lateral Raise (Cable)",
      hevyExerciseTemplateId: "BE289E45",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 4,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
    },
    // Bicep Curl (Cable) - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Bicep Curl (Cable)",
      hevyExerciseTemplateId: "ADA8623C",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 5,
      startingWeight: 15,
      weightUnit: WeightUnit.Kilograms,
    },
    // Triceps Pushdown - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Triceps Pushdown",
      hevyExerciseTemplateId: "93A552C6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 6,
      startingWeight: 20,
      weightUnit: WeightUnit.Kilograms,
    },
    // Rear Delt Reverse Fly (Cable) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Rear Delt Reverse Fly (Cable)",
      hevyExerciseTemplateId: "C315DC2A",
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
      templateName: "Squat (Smith Machine)",
      hevyExerciseTemplateId: "DDCC3821",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 2,
      orderInDay: 1,
      trainingMaxValue: 107.5,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Lunge (Barbell) - RepsPerSet (4 sets x 9 reps)
    {
      templateName: "Lunge (Barbell)",
      hevyExerciseTemplateId: "6E6EE645",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 2,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Lying Leg Curl (Machine) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Lying Leg Curl (Machine)",
      hevyExerciseTemplateId: "B8127AD1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 3,
      startingWeight: 30,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Abduction (Machine) - RepsPerSet (3 sets x 12 reps)
    {
      templateName: "Hip Abduction (Machine)",
      hevyExerciseTemplateId: "F4B4C6EE",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 4,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Calf Press (Machine) - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "Calf Press (Machine)",
      hevyExerciseTemplateId: "91237BDD",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 5,
      startingWeight: 60,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 3 ====================
    // Triceps Dip (Assisted) - RepsPerSet (3 sets, 40 total reps)
    {
      templateName: "Triceps Dip (Assisted)",
      hevyExerciseTemplateId: "4B4BF8C2",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 1,
      startingWeight: 32,
      weightUnit: WeightUnit.Kilograms,
    },
    // Pull Up (Assisted) - RepsPerSet (6 sets, 40 total reps)
    {
      templateName: "Pull Up (Assisted)",
      hevyExerciseTemplateId: "2C37EC5E",
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
      hevyExerciseTemplateId: "724CDE60",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 3,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // EZ Bar Biceps Curl - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "EZ Bar Biceps Curl",
      hevyExerciseTemplateId: "01A35BF9",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 4,
      startingWeight: 20,
      weightUnit: WeightUnit.Kilograms,
    },
    // Single Arm Triceps Pushdown (Cable) - RepsPerSet (6 sets x 25 reps)
    {
      templateName: "Single Arm Triceps Pushdown (Cable)",
      hevyExerciseTemplateId: "552AB030",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 5,
      startingWeight: 10,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Lateral Raise (Dumbbell) - RepsPerSet (3 sets x 20 reps)
    {
      templateName: "Lateral Raise (Dumbbell)",
      hevyExerciseTemplateId: "422B08F1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 6,
      startingWeight: 8,
      weightUnit: WeightUnit.Kilograms,
    },
    // Chest Fly (Machine) - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Chest Fly (Machine)",
      hevyExerciseTemplateId: "78683336",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 7,
      startingWeight: 15,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 4 ====================
    // Hip Thrust (Machine) - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Hip Thrust (Machine)",
      hevyExerciseTemplateId: "68CE0B9B",
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
      hevyExerciseTemplateId: "5046D0A9",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 4,
      orderInDay: 2,
      trainingMaxValue: 80,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Single Leg Press (Machine) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Single Leg Press (Machine)",
      hevyExerciseTemplateId: "3FD83744",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 3,
      startingWeight: 80,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Leg Extension (Machine) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Leg Extension (Machine)",
      hevyExerciseTemplateId: "75A4F6C4",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 4,
      startingWeight: 40,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Adduction (Machine) - RepsPerSet (4 sets)
    {
      templateName: "Hip Adduction (Machine)",
      hevyExerciseTemplateId: "8BEBFED6",
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
