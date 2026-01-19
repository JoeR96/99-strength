import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ExerciseSelectionV2 } from "./ExerciseSelectionV2";
import {
  EquipmentType,
  ExerciseCategory,
  ProgramVariant,
  type ExerciseTemplate,
  type SelectedExercise,
} from "../../../types/workout";
import { workoutsApi } from "../../../api/workouts";

// Create a fresh query client for each story
const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Infinity,
      },
    },
  });

// Mock exercise templates (comprehensive library)
const mockTemplates: ExerciseTemplate[] = [
  // Main Lifts
  {
    name: "Squat",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Back Squat - primary lower body compound movement",
  },
  {
    name: "Bench Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Barbell Bench Press - primary chest compound movement",
  },
  {
    name: "Deadlift",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 1, target: 3, maximum: 5 },
    defaultSets: 3,
    description: "Conventional Deadlift - primary posterior chain movement",
  },
  {
    name: "Overhead Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Standing Overhead Press - primary shoulder movement",
  },
  // Barbell Compounds (Auxiliary)
  {
    name: "Front Squat",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
    defaultSets: 3,
    description: "Front-loaded squat variation",
  },
  {
    name: "Romanian Deadlift",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 10, maximum: 12 },
    defaultSets: 3,
    description: "Hip-hinge focused deadlift variation",
  },
  {
    name: "Barbell Row",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 10, maximum: 12 },
    defaultSets: 4,
    description: "Bent-over barbell row for back thickness",
  },
  {
    name: "Incline Bench Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 3,
    description: "Upper chest focused pressing movement",
  },
  {
    name: "Pause Squat",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 3,
    description: "Squat with pause at bottom for strength development",
  },
  {
    name: "Close Grip Bench Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 3,
    description: "Tricep-focused bench variation",
  },
  // Dumbbell Exercises (Accessory)
  {
    name: "Dumbbell Row",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Single-arm rowing movement",
  },
  {
    name: "Dumbbell Bench Press",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Chest press with dumbbells",
  },
  {
    name: "Dumbbell Shoulder Press",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Seated or standing shoulder press",
  },
  {
    name: "Dumbbell Curl",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 10, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Bicep isolation exercise",
  },
  {
    name: "Dumbbell Lateral Raise",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Side deltoid isolation",
  },
  // Cable Exercises
  {
    name: "Cable Row",
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Seated cable row for back",
  },
  {
    name: "Cable Fly",
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Chest isolation with cables",
  },
  {
    name: "Cable Tricep Extension",
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Tricep isolation exercise",
  },
  {
    name: "Face Pull",
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 15, target: 20, maximum: 25 },
    defaultSets: 3,
    description: "Rear deltoid and upper back exercise",
  },
  // Machine Exercises
  {
    name: "Leg Press",
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Leg press machine for quadriceps",
  },
  {
    name: "Leg Curl",
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 10, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Hamstring isolation",
  },
  {
    name: "Leg Extension",
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 10, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Quadriceps isolation",
  },
  {
    name: "Chest Press Machine",
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Machine chest press",
  },
  // Bodyweight Exercises
  {
    name: "Pull-up",
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 5, target: 10, maximum: 15 },
    defaultSets: 3,
    description: "Bodyweight vertical pull",
  },
  {
    name: "Chin-up",
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 5, target: 10, maximum: 15 },
    defaultSets: 3,
    description: "Underhand grip pull-up",
  },
  {
    name: "Dip",
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Bodyweight chest and tricep exercise",
  },
  {
    name: "Push-up",
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 15, target: 20, maximum: 30 },
    defaultSets: 3,
    description: "Bodyweight chest press",
  },
];

// Mock selected exercises
const mockMainLiftsSelected: SelectedExercise[] = [
  {
    id: "1",
    template: mockTemplates[0], // Squat
    category: ExerciseCategory.MainLift,
    progressionType: "Linear",
    assignedDay: 1,
    orderInDay: 1,
  },
  {
    id: "2",
    template: mockTemplates[1], // Bench
    category: ExerciseCategory.MainLift,
    progressionType: "Linear",
    assignedDay: 2,
    orderInDay: 1,
  },
  {
    id: "3",
    template: mockTemplates[2], // Deadlift
    category: ExerciseCategory.MainLift,
    progressionType: "Linear",
    assignedDay: 3,
    orderInDay: 1,
  },
  {
    id: "4",
    template: mockTemplates[3], // OHP
    category: ExerciseCategory.MainLift,
    progressionType: "Linear",
    assignedDay: 4,
    orderInDay: 1,
  },
];

const mockMixedSelection: SelectedExercise[] = [
  ...mockMainLiftsSelected,
  {
    id: "5",
    template: mockTemplates[6], // Barbell Row
    category: ExerciseCategory.Auxiliary,
    progressionType: "Linear",
    assignedDay: 1,
    orderInDay: 2,
  },
  {
    id: "6",
    template: mockTemplates[5], // Romanian Deadlift
    category: ExerciseCategory.Auxiliary,
    progressionType: "Linear",
    assignedDay: 2,
    orderInDay: 2,
  },
  {
    id: "7",
    template: mockTemplates[10], // Dumbbell Row
    category: ExerciseCategory.Accessory,
    progressionType: "RepsPerSet",
    assignedDay: 1,
    orderInDay: 3,
  },
  {
    id: "8",
    template: mockTemplates[14], // Lateral Raise
    category: ExerciseCategory.Accessory,
    progressionType: "RepsPerSet",
    assignedDay: 2,
    orderInDay: 3,
  },
];

// Mock the API before creating stories
if (typeof window !== 'undefined') {
  workoutsApi.getExerciseLibrary = async () => ({
    templates: mockTemplates,
  });
}

const meta = {
  title: "Features/Workout/ExerciseSelectionV2",
  component: ExerciseSelectionV2,
  parameters: {
    layout: "padded",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof ExerciseSelectionV2>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * 1. Default - Empty selection, full library
 */
export const Default: Story = {
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 2. WithMainLiftsSelected - 4 main lifts configured
 */
export const WithMainLiftsSelected: Story = {
  args: {
    selectedExercises: mockMainLiftsSelected,
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 3. WithMixedSelection - Main + Auxiliary + Accessory
 */
export const WithMixedSelection: Story = {
  args: {
    selectedExercises: mockMixedSelection,
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 4. FourDayProgram - Exercises spread across 4 days
 */
export const FourDayProgram: Story = {
  args: {
    selectedExercises: mockMixedSelection,
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 5. SixDayProgram - High frequency setup
 */
export const SixDayProgram: Story = {
  args: {
    selectedExercises: [
      {
        id: "1",
        template: mockTemplates[0],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 1,
        orderInDay: 1,
      },
      {
        id: "2",
        template: mockTemplates[1],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: "3",
        template: mockTemplates[2],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 4,
        orderInDay: 1,
      },
      {
        id: "4",
        template: mockTemplates[3],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 5,
        orderInDay: 1,
      },
      {
        id: "5",
        template: mockTemplates[0],
        category: ExerciseCategory.Auxiliary,
        progressionType: "Linear",
        assignedDay: 3,
        orderInDay: 1,
      },
      {
        id: "6",
        template: mockTemplates[1],
        category: ExerciseCategory.Auxiliary,
        progressionType: "Linear",
        assignedDay: 6,
        orderInDay: 1,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.SixDay,
  },
};

/**
 * 6. ThreeDayProgram - Full body approach
 */
export const ThreeDayProgram: Story = {
  args: {
    selectedExercises: [
      {
        id: "1",
        template: mockTemplates[0],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 1,
        orderInDay: 1,
      },
      {
        id: "2",
        template: mockTemplates[1],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 1,
        orderInDay: 2,
      },
      {
        id: "3",
        template: mockTemplates[2],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: "4",
        template: mockTemplates[3],
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 3,
        orderInDay: 1,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 7. MaximalSelection - Many exercises selected
 */
export const MaximalSelection: Story = {
  args: {
    selectedExercises: [
      ...mockMixedSelection,
      {
        id: "9",
        template: mockTemplates[15], // Cable Row
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 2,
      },
      {
        id: "10",
        template: mockTemplates[18], // Face Pull
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 3,
      },
      {
        id: "11",
        template: mockTemplates[19], // Leg Press
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 4,
        orderInDay: 2,
      },
      {
        id: "12",
        template: mockTemplates[23], // Pull-up
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 4,
        orderInDay: 3,
      },
      {
        id: "13",
        template: mockTemplates[13], // Dumbbell Curl
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 4,
      },
      {
        id: "14",
        template: mockTemplates[17], // Cable Tricep Extension
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 4,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 8. EmptyState - No exercises selected
 */
export const EmptyState: Story = {
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 9. OnlyAccessoryExercises - RepsPerSet progression only
 */
export const OnlyAccessoryExercises: Story = {
  args: {
    selectedExercises: [
      {
        id: "1",
        template: mockTemplates[10], // Dumbbell Row
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 1,
      },
      {
        id: "2",
        template: mockTemplates[11], // Dumbbell Bench
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 2,
      },
      {
        id: "3",
        template: mockTemplates[23], // Pull-up
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: "4",
        template: mockTemplates[25], // Dip
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 2,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 10. OnlyBarbellExercises - Powerlifting focused
 */
export const OnlyBarbellExercises: Story = {
  args: {
    selectedExercises: [
      {
        id: "1",
        template: mockTemplates[0], // Squat
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 1,
        orderInDay: 1,
      },
      {
        id: "2",
        template: mockTemplates[1], // Bench
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: "3",
        template: mockTemplates[2], // Deadlift
        category: ExerciseCategory.MainLift,
        progressionType: "Linear",
        assignedDay: 3,
        orderInDay: 1,
      },
      {
        id: "4",
        template: mockTemplates[8], // Pause Squat
        category: ExerciseCategory.Auxiliary,
        progressionType: "Linear",
        assignedDay: 2,
        orderInDay: 2,
      },
      {
        id: "5",
        template: mockTemplates[9], // Close Grip Bench
        category: ExerciseCategory.Auxiliary,
        progressionType: "Linear",
        assignedDay: 3,
        orderInDay: 2,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 11. BodyweightOnly - Calisthenics focused
 */
export const BodyweightOnly: Story = {
  args: {
    selectedExercises: [
      {
        id: "1",
        template: mockTemplates[23], // Pull-up
        category: ExerciseCategory.MainLift,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 1,
      },
      {
        id: "2",
        template: mockTemplates[25], // Dip
        category: ExerciseCategory.MainLift,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: "3",
        template: mockTemplates[24], // Chin-up
        category: ExerciseCategory.Auxiliary,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 1,
      },
      {
        id: "4",
        template: mockTemplates[26], // Push-up
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 2,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 12. MobileView - Responsive layout test
 */
export const MobileView: Story = {
  args: {
    selectedExercises: mockMixedSelection,
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FourDay,
  },
  parameters: {
    viewport: {
      defaultViewport: "mobile1",
    },
  },
};

/**
 * 13. FiveDayTraditionalSplit - Complete 5-day bodybuilding split with all progression data
 * Day 1: Chest
 * Day 2: Back
 * Day 3: Shoulders
 * Day 4: Legs
 * Day 5: Arms
 */
export const FiveDayTraditionalSplit: Story = {
  args: {
    selectedExercises: [
      // DAY 1 - CHEST
      {
        id: "chest-1",
        template: mockTemplates[1], // Bench Press
        category: ExerciseCategory.MainLift,
        progressionType: "Hypertrophy",
        assignedDay: 1,
        orderInDay: 1,
        trainingMax: { value: 100, unit: 0 }, // 100kg
        isPrimary: true,
        baseSetsPerExercise: 4,
      },
      {
        id: "chest-2",
        template: mockTemplates[7], // Incline Bench Press
        category: ExerciseCategory.Auxiliary,
        progressionType: "Hypertrophy",
        assignedDay: 1,
        orderInDay: 2,
        trainingMax: { value: 80, unit: 0 }, // 80kg
        isPrimary: false,
        baseSetsPerExercise: 3,
      },
      {
        id: "chest-3",
        template: mockTemplates[11], // Dumbbell Bench Press
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 3,
        repRange: { minimum: 8, target: 10, maximum: 12 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 30,
        weightUnit: 0,
      },
      {
        id: "chest-4",
        template: mockTemplates[16], // Cable Fly
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 4,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 20,
        weightUnit: 0,
      },
      {
        id: "chest-5",
        template: mockTemplates[25], // Dip
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 1,
        orderInDay: 5,
        repRange: { minimum: 8, target: 10, maximum: 12 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 0, // Bodyweight
        weightUnit: 0,
      },
      // DAY 2 - BACK
      {
        id: "back-1",
        template: mockTemplates[2], // Deadlift
        category: ExerciseCategory.MainLift,
        progressionType: "Hypertrophy",
        assignedDay: 2,
        orderInDay: 1,
        trainingMax: { value: 140, unit: 0 }, // 140kg
        isPrimary: true,
        baseSetsPerExercise: 3,
      },
      {
        id: "back-2",
        template: mockTemplates[6], // Barbell Row
        category: ExerciseCategory.Auxiliary,
        progressionType: "Hypertrophy",
        assignedDay: 2,
        orderInDay: 2,
        trainingMax: { value: 90, unit: 0 }, // 90kg
        isPrimary: false,
        baseSetsPerExercise: 4,
      },
      {
        id: "back-3",
        template: mockTemplates[23], // Pull-up
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 3,
        repRange: { minimum: 6, target: 8, maximum: 10 },
        currentSets: 3,
        targetSets: 5,
        startingWeight: 0, // Bodyweight
        weightUnit: 0,
      },
      {
        id: "back-4",
        template: mockTemplates[15], // Cable Row
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 4,
        repRange: { minimum: 10, target: 12, maximum: 15 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 60,
        weightUnit: 0,
      },
      {
        id: "back-5",
        template: mockTemplates[18], // Face Pull
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 2,
        orderInDay: 5,
        repRange: { minimum: 15, target: 20, maximum: 25 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 15,
        weightUnit: 0,
      },
      // DAY 3 - SHOULDERS
      {
        id: "shoulders-1",
        template: mockTemplates[3], // Overhead Press
        category: ExerciseCategory.MainLift,
        progressionType: "Hypertrophy",
        assignedDay: 3,
        orderInDay: 1,
        trainingMax: { value: 60, unit: 0 }, // 60kg
        isPrimary: true,
        baseSetsPerExercise: 4,
      },
      {
        id: "shoulders-2",
        template: mockTemplates[12], // Dumbbell Shoulder Press
        category: ExerciseCategory.Auxiliary,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 2,
        repRange: { minimum: 8, target: 10, maximum: 12 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 20,
        weightUnit: 0,
      },
      {
        id: "shoulders-3",
        template: mockTemplates[14], // Dumbbell Lateral Raise
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 3,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 10,
        weightUnit: 0,
      },
      {
        id: "shoulders-4",
        template: mockTemplates[18], // Face Pull
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 4,
        repRange: { minimum: 15, target: 20, maximum: 25 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 15,
        weightUnit: 0,
      },
      {
        id: "shoulders-5",
        template: mockTemplates[12], // Dumbbell Shoulder Press (second variation)
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 3,
        orderInDay: 5,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 15,
        weightUnit: 0,
      },
      // DAY 4 - LEGS
      {
        id: "legs-1",
        template: mockTemplates[0], // Squat
        category: ExerciseCategory.MainLift,
        progressionType: "Hypertrophy",
        assignedDay: 4,
        orderInDay: 1,
        trainingMax: { value: 120, unit: 0 }, // 120kg
        isPrimary: true,
        baseSetsPerExercise: 4,
      },
      {
        id: "legs-2",
        template: mockTemplates[5], // Romanian Deadlift
        category: ExerciseCategory.Auxiliary,
        progressionType: "Hypertrophy",
        assignedDay: 4,
        orderInDay: 2,
        trainingMax: { value: 100, unit: 0 }, // 100kg
        isPrimary: false,
        baseSetsPerExercise: 3,
      },
      {
        id: "legs-3",
        template: mockTemplates[19], // Leg Press
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 4,
        orderInDay: 3,
        repRange: { minimum: 10, target: 12, maximum: 15 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 100,
        weightUnit: 0,
      },
      {
        id: "legs-4",
        template: mockTemplates[20], // Leg Curl
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 4,
        orderInDay: 4,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 40,
        weightUnit: 0,
      },
      {
        id: "legs-5",
        template: mockTemplates[21], // Leg Extension
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 4,
        orderInDay: 5,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 50,
        weightUnit: 0,
      },
      // DAY 5 - ARMS
      {
        id: "arms-1",
        template: mockTemplates[9], // Close Grip Bench Press
        category: ExerciseCategory.Auxiliary,
        progressionType: "Hypertrophy",
        assignedDay: 5,
        orderInDay: 1,
        trainingMax: { value: 70, unit: 0 }, // 70kg
        isPrimary: false,
        baseSetsPerExercise: 3,
      },
      {
        id: "arms-2",
        template: mockTemplates[13], // Dumbbell Curl
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 5,
        orderInDay: 2,
        repRange: { minimum: 10, target: 12, maximum: 15 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 15,
        weightUnit: 0,
      },
      {
        id: "arms-3",
        template: mockTemplates[17], // Cable Tricep Extension
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 5,
        orderInDay: 3,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 25,
        weightUnit: 0,
      },
      {
        id: "arms-4",
        template: mockTemplates[13], // Dumbbell Curl (hammer variation)
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 5,
        orderInDay: 4,
        repRange: { minimum: 12, target: 15, maximum: 20 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 12,
        weightUnit: 0,
      },
      {
        id: "arms-5",
        template: mockTemplates[17], // Cable Tricep Extension (overhead variation)
        category: ExerciseCategory.Accessory,
        progressionType: "RepsPerSet",
        assignedDay: 5,
        orderInDay: 5,
        repRange: { minimum: 15, target: 20, maximum: 25 },
        currentSets: 3,
        targetSets: 4,
        startingWeight: 20,
        weightUnit: 0,
      },
    ],
    onUpdate: (exercises) => console.log("Updated exercises:", exercises),
    programVariant: ProgramVariant.FiveDay,
  },
};
