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
  blockSequence: number[];
  exercises: CreateExerciseRequest[];
}

/**
 * 4-Day Hypertrophy Template
 * Based on the A2S 2024-2025 program spreadsheet
 * - Linear progression for main lifts (Overhead Press, Smith Squat, Front Squat)
 * - RepsPerSet progression for accessories
 *
 * IMPORTANT: templateName must match exactly with ExerciseLibrary names
 * externalTemplateId must match Hevy's exercise IDs from hevyExercises.ts
 */
const fourDayHypertrophyTemplate: WorkoutTemplate = {
  id: 'four-day-hypertrophy',
  name: '4-Day Hypertrophy',
  description: 'A balanced 4-day split focusing on hypertrophy with 3 main lifts and targeted accessories.',
  variant: 4,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  exercises: [
    // ==================== DAY 1 ====================
    // Lat Pulldown - RepsPerSet (3 sets x 12 reps -> 5 sets)
    {
      templateName: "Lat Pulldown (Cable)",
      externalTemplateId: "6A6C31A5",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
    },
    // Overhead Press (Smith Machine) - Linear (TM: 65kg)
    {
      templateName: "Overhead Press (Smith Machine)",
      externalTemplateId: "B09A1304",
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
      externalTemplateId: "0393F233",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
    },
    // Lateral Raise (Cable) - RepsPerSet (4 sets x 8 reps)
    {
      templateName: "Lateral Raise (Cable)",
      externalTemplateId: "BE289E45",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
    },
    // Bicep Curl (Cable) - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Bicep Curl (Cable)",
      externalTemplateId: "ADA8623C",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
    },
    // Triceps Pushdown - RepsPerSet (4 sets x 20 reps)
    {
      templateName: "Triceps Pushdown",
      externalTemplateId: "93A552C6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
    },
    // Rear Delt Reverse Fly (Cable) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Rear Delt Reverse Fly (Cable)",
      externalTemplateId: "C315DC2A",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 7,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 2 ====================
    // Smith Squat - Linear (TM: 107.5kg)
    {
      templateName: "Squat (Smith Machine)",
      externalTemplateId: "DDCC3821",
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
      externalTemplateId: "6E6EE645",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Lying Leg Curl (Machine) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Lying Leg Curl (Machine)",
      externalTemplateId: "B8127AD1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Abduction (Machine) - RepsPerSet (3 sets x 12 reps)
    {
      templateName: "Hip Abduction (Machine)",
      externalTemplateId: "F4B4C6EE",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
    },
    // Calf Press (Machine) - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "Calf Press (Machine)",
      externalTemplateId: "91237BDD",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 3 ====================
    // Triceps Dip (Assisted) - RepsPerSet (3 sets, 40 total reps)
    {
      templateName: "Triceps Dip (Assisted)",
      externalTemplateId: "4B4BF8C2",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
    },
    // Pull Up (Assisted) - RepsPerSet (6 sets, 40 total reps)
    {
      templateName: "Pull Up (Assisted)",
      externalTemplateId: "2C37EC5E",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
    },
    // Concentration Curl - RepsPerSet (4 sets x 15 reps)
    {
      templateName: "Concentration Curl",
      externalTemplateId: "724CDE60",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // EZ Bar Biceps Curl - RepsPerSet (3 sets x 15 reps)
    {
      templateName: "EZ Bar Biceps Curl",
      externalTemplateId: "01A35BF9",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
    },
    // Single Arm Triceps Pushdown (Cable) - RepsPerSet (6 sets x 25 reps)
    {
      templateName: "Single Arm Triceps Pushdown (Cable)",
      externalTemplateId: "552AB030",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Lateral Raise (Dumbbell) - RepsPerSet (3 sets x 20 reps)
    {
      templateName: "Lateral Raise (Dumbbell)",
      externalTemplateId: "422B08F1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
    },
    // Chest Fly (Machine) - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Chest Fly (Machine)",
      externalTemplateId: "78683336",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 7,
      weightUnit: WeightUnit.Kilograms,
    },

    // ==================== DAY 4 ====================
    // Hip Thrust (Machine) - RepsPerSet (3 sets x 8 reps)
    {
      templateName: "Hip Thrust (Machine)",
      externalTemplateId: "68CE0B9B",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
    },
    // Front Squat - Linear (TM: 80kg)
    {
      templateName: "Front Squat",
      externalTemplateId: "5046D0A9",
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
      externalTemplateId: "3FD83744",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      isUnilateral: true,
    },
    // Leg Extension (Machine) - RepsPerSet (4 sets x 12 reps)
    {
      templateName: "Leg Extension (Machine)",
      externalTemplateId: "75A4F6C4",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
    },
    // Hip Adduction (Machine) - RepsPerSet (4 sets)
    {
      templateName: "Hip Adduction (Machine)",
      externalTemplateId: "8BEBFED6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
    },
  ] as CreateExerciseRequest[],
};

/**
 * Monk Mode Template
 * A 4-day Upper/Lower split with posterior chain focus
 * - Day 1: Upper Push/Pull
 * - Day 2: Legs (Posterior Focus)
 * - Day 3: Upper Pull/Arms
 * - Day 4: Legs (Quad Focus)
 *
 * Main lifts use Linear progression with AMRAP on last set
 * Accessories use RepsPerSet progression
 * Assisted exercises use MinimalSets progression
 *
 * Starting weights based on Hevy training history
 */
const monkModeTemplate: WorkoutTemplate = {
  id: 'monk-mode',
  name: 'Monk Mode',
  description: 'A focused 4-day Upper/Lower split emphasizing compound movements with linear progression on main lifts. Updated from Hevy workout data.',
  variant: 4,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  exercises: [
    // ==================== DAY 1: Upper Push/Pull ====================
    // Overhead Press (Smith Machine) - Linear (TM: 65kg)
    // From Hevy Week 2 Day 1: working weight 56.3kg at 85% → TM ≈ 65kg
    {
      templateName: "Overhead Press (Smith Machine)",
      externalTemplateId: "B09A1304",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 1,
      orderInDay: 1,
      trainingMaxValue: 65,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Bent Over Row (Barbell) - RepsPerSet (3 sets)
    {
      templateName: "Bent Over Row (Barbell)",
      externalTemplateId: "55E6546F",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Hanging Knee Raise - Bodyweight core work (3 sets)
    {
      templateName: "Hanging Knee Raise",
      externalTemplateId: "08590920",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Lateral Raise (Cable) - RepsPerSet (6 sets from Hevy)
    {
      templateName: "Lateral Raise (Cable)",
      externalTemplateId: "BE289E45",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 6,
      targetSets: 8,
    },
    // Bicep Curl (Cable) - RepsPerSet (3 sets)
    {
      templateName: "Bicep Curl (Cable)",
      externalTemplateId: "ADA8623C",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Crucifix Tricep Pulldown - RepsPerSet (3 sets)
    {
      templateName: "Crucifix Tricep Pulldown",
      externalTemplateId: "4296b371-d566-46c0-8c63-88fc3e97054a",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },

    // ==================== DAY 2: Legs (Posterior Focus) ====================
    // Squat (Barbell) - Linear (TM: 110kg)
    // From Hevy Week 2 Day 2: working weight 92.5kg at 85% → TM ≈ 110kg
    {
      templateName: "Squat (Barbell)",
      externalTemplateId: "D04AC939",
      category: ExerciseCategory.MainLift,
      progressionType: "Linear",
      assignedDay: 2,
      orderInDay: 1,
      trainingMaxValue: 110,
      trainingMaxUnit: WeightUnit.Kilograms,
    },
    // Romanian Deadlift (Dumbbell) - RepsPerSet (3 sets, unilateral)
    {
      templateName: "Romanian Deadlift (Dumbbell)",
      externalTemplateId: "72CFFAD5",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
      isUnilateral: true,
    },
    // Hip Abduction (Machine) - RepsPerSet (4 sets)
    {
      templateName: "Hip Abduction (Machine)",
      externalTemplateId: "F4B4C6EE",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Leg Extension (Machine) - RepsPerSet (3 sets)
    {
      templateName: "Leg Extension (Machine)",
      externalTemplateId: "75A4F6C4",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Calf Press (Machine) - RepsPerSet (4 sets)
    {
      templateName: "Calf Press (Machine)",
      externalTemplateId: "91237BDD",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },

    // ==================== DAY 3: Upper Pull/Arms ====================
    // Single Arm Lat Pulldown - RepsPerSet (4 sets, UNILATERAL)
    {
      templateName: "Single Arm Lat Pulldown",
      externalTemplateId: "2EE45F81",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      isUnilateral: true,
    },
    // Overhead Press (Barbell) - RepsPerSet (3 sets)
    {
      templateName: "Overhead Press (Barbell)",
      externalTemplateId: "7B8D84E8",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Lateral Raise (Dumbbell) - RepsPerSet (3 sets)
    {
      templateName: "Lateral Raise (Dumbbell)",
      externalTemplateId: "422B08F1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Single Arm Cable Row - RepsPerSet (4 sets)
    {
      templateName: "Single Arm Cable Row",
      externalTemplateId: "D0C4A899",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // EZ Bar Biceps Curl - RepsPerSet (2 sets)
    {
      templateName: "EZ Bar Biceps Curl",
      externalTemplateId: "01A35BF9",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 2,
      targetSets: 4,
    },
    // Skullcrusher (Barbell) - RepsPerSet (3 sets)
    {
      templateName: "Skullcrusher (Barbell)",
      externalTemplateId: "875F585F",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },

    // ==================== DAY 4: Legs (Quad Focus) ====================
    // Pause Squat (Barbell) - RepsPerSet (3 sets, rep range 3-4-5)
    {
      templateName: "Pause Squat (Barbell)",
      externalTemplateId: "CE1054CE",
      category: ExerciseCategory.MainLift,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
      repRangeMinimum: 3,
      repRangeMaximum: 5,
    },
    // Hip Thrust (Machine) - RepsPerSet (3 sets)
    {
      templateName: "Hip Thrust (Machine)",
      externalTemplateId: "68CE0B9B",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
    // Hip Adduction (Machine) - RepsPerSet (3 sets)
    {
      templateName: "Hip Adduction (Machine)",
      externalTemplateId: "8BEBFED6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
  ] as CreateExerciseRequest[],
};

/**
 * Optimised 4-Day Hybrid Split V2
 * Designed for sedentary software engineers with suspected upper/lower cross syndrome
 * - Day 1: Lower (Quad Strength / Knee Dominant)
 * - Day 2: Upper (Push + Pull Accent)
 * - Day 3: Lower (Posterior Chain & Realignment)
 * - Day 4: Upper (Pull + Push Accent)
 *
 * No Linear progression — all exercises use RepsPerSet
 * Includes corrective exercises (Pallof Press, Hip Ab/Adduction)
 */
const optimised4DayV2Template: WorkoutTemplate = {
  id: 'optimised-4day-v2',
  name: 'Optimised 4-Day Hybrid V2',
  description: 'A corrective-focused 4-day hybrid split for desk workers. Targets upper/lower cross syndrome with quad/posterior chain days and push/pull upper days.',
  variant: 4,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  exercises: [
    // ==================== DAY 1: Lower — Quad Strength (Knee Dominant) ====================
    // Back Squat - Main lift (4 sets x 8-10)
    {
      templateName: "Squat (Barbell)",
      externalTemplateId: "D04AC939",
      category: ExerciseCategory.MainLift,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 8,
      repRangeMaximum: 12,
    },
    // Smith Squat Lunge (4 sets x 10-12 /leg, unilateral)
    {
      templateName: "Single Leg Smith Lunge",
      externalTemplateId: "1ae9ac4c-da64-47ac-95ad-5e738299827a",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      isUnilateral: true,
    },
    // Lying Leg Curl (4 sets x 10-12)
    {
      templateName: "Lying Leg Curl (Machine)",
      externalTemplateId: "B8127AD1",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Hip Abduction (4 sets x 12-15)
    {
      templateName: "Hip Abduction (Machine)",
      externalTemplateId: "F4B4C6EE",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Pallof Press (3 sets x 8-10 /side, unilateral)
    {
      templateName: "Cable Core Palloff Press",
      externalTemplateId: "CC55119B",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
      isUnilateral: true,
    },
    // Calf Raises (4 sets x 15-20)
    {
      templateName: "Standing Calf Raise (Machine)",
      externalTemplateId: "E05C2C38",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 1,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 15,
      repRangeMaximum: 25,
    },

    // ==================== DAY 2: Upper — Push + Pull Accent ====================
    // Incline Smith Press - Main lift (4 sets x 8-10)
    {
      templateName: "Incline Bench Press (Smith Machine)",
      externalTemplateId: "3A6FA3D1",
      category: ExerciseCategory.MainLift,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Overhead DB Press (4 sets x 8-10)
    {
      templateName: "Overhead Press (Dumbbell)",
      externalTemplateId: "6AC96645",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Single Arm Cable Lat Pulldown (4 sets x 10-12 /arm, unilateral)
    {
      templateName: "Single Arm Lat Pulldown",
      externalTemplateId: "2EE45F81",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      isUnilateral: true,
    },
    // Cable Lateral Raise (4 sets x 12-15)
    {
      templateName: "Lateral Raise (Cable)",
      externalTemplateId: "BE289E45",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Chest Flye (4 sets x 10-12)
    {
      templateName: "Chest Fly (Machine)",
      externalTemplateId: "78683336",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Cable Tricep Pushdown (4 sets x 10-12)
    {
      templateName: "Triceps Pushdown",
      externalTemplateId: "93A552C6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 2,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },

    // ==================== DAY 3: Lower — Posterior Chain & Realignment ====================
    // Pause Squat (4 sets x 5-8)
    {
      templateName: "Pause Squat (Barbell)",
      externalTemplateId: "CE1054CE",
      category: ExerciseCategory.MainLift,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 5,
      repRangeMaximum: 10,
    },
    // Romanian Deadlift (4 sets x 8-10)
    {
      templateName: "Romanian Deadlift (Barbell)",
      externalTemplateId: "2B4B7310",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Booty Builder / Hip Thrust (4 sets x 12-15)
    {
      templateName: "Hip Thrust (Machine)",
      externalTemplateId: "68CE0B9B",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Hip Adduction (4 sets x 12-15)
    {
      templateName: "Hip Adduction (Machine)",
      externalTemplateId: "8BEBFED6",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Hip Abduction (4 sets x 12-15)
    {
      templateName: "Hip Abduction (Machine)",
      externalTemplateId: "F4B4C6EE",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Pallof Press (3 sets x 8-10 /side, unilateral)
    {
      templateName: "Cable Core Palloff Press",
      externalTemplateId: "CC55119B",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 3,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
      isUnilateral: true,
    },

    // ==================== DAY 4: Upper — Pull + Push Accent ====================
    // Assisted Pull-ups (4 sets x 6-10)
    {
      templateName: "Pull Up (Assisted)",
      externalTemplateId: "2C37EC5E",
      category: ExerciseCategory.MainLift,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 1,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 6,
      repRangeMaximum: 12,
    },
    // Assisted Dips (4 sets x 8-10)
    {
      templateName: "Triceps Dip (Assisted)",
      externalTemplateId: "4B4BF8C2",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 2,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Cable Low Row (4 sets x 10-12)
    {
      templateName: "Seated Cable Row - V Grip (Cable)",
      externalTemplateId: "0393F233",
      category: ExerciseCategory.Auxiliary,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 3,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
    },
    // Rear Delt Flyes (4 sets x 12-15)
    {
      templateName: "Rear Delt Reverse Fly (Dumbbell)",
      externalTemplateId: "E5988A0A",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 4,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 4,
      targetSets: 6,
      repRangeMinimum: 12,
      repRangeMaximum: 18,
    },
    // Concentration Curl (3 sets x 10-12 /arm, unilateral)
    {
      templateName: "Concentration Curl",
      externalTemplateId: "724CDE60",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 5,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
      isUnilateral: true,
    },
    // Skullcrushers (3 sets x 10-12)
    {
      templateName: "Skullcrusher (Barbell)",
      externalTemplateId: "875F585F",
      category: ExerciseCategory.Accessory,
      progressionType: "RepsPerSet",
      assignedDay: 4,
      orderInDay: 6,
      weightUnit: WeightUnit.Kilograms,
      startingSets: 3,
      targetSets: 5,
    },
  ] as CreateExerciseRequest[],
};

/**
 * All available workout templates
 */
export const workoutTemplates: WorkoutTemplate[] = [
  fourDayHypertrophyTemplate,
  monkModeTemplate,
  optimised4DayV2Template,
];

/**
 * Get a template by ID
 */
export function getTemplateById(id: string): WorkoutTemplate | undefined {
  return workoutTemplates.find(t => t.id === id);
}
