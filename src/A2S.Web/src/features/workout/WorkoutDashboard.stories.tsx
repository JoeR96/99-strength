import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ClerkProvider } from "@clerk/clerk-react";
import { WorkoutDashboard } from "./WorkoutDashboard";
import { workoutsApi } from "../../api/workouts";
import type { WorkoutDto, ExerciseDto } from "../../types/workout";
import { WeightUnit, ExerciseCategory, EquipmentType } from "../../types/workout";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

const makeLinearExercise = (
  id: string,
  name: string,
  day: number,
  order: number,
  tmValue: number
): ExerciseDto => ({
  id,
  name,
  category: ExerciseCategory.MainLift,
  equipment: EquipmentType.Barbell,
  assignedDay: day as ExerciseDto["assignedDay"],
  orderInDay: order,
  hevyExerciseTemplateId: `${name.toLowerCase().replace(/\s/g, "-")}-id`,
  progression: {
    type: "Linear",
    trainingMax: { value: tmValue, unit: WeightUnit.Kilograms },
    useAmrap: true,
    baseSetsPerExercise: 5,
  },
});

const makeRpsExercise = (
  id: string,
  name: string,
  day: number,
  order: number,
  weight: number
): ExerciseDto => ({
  id,
  name,
  category: ExerciseCategory.Accessory,
  equipment: EquipmentType.Dumbbell,
  assignedDay: day as ExerciseDto["assignedDay"],
  orderInDay: order,
  hevyExerciseTemplateId: `${name.toLowerCase().replace(/\s/g, "-")}-id`,
  progression: {
    type: "RepsPerSet",
    repRange: { minimum: 10, maximum: 15 },
    startingSets: 3,
    currentSetCount: 4,
    targetSets: 5,
    currentWeight: weight,
    weightUnit: "Kilograms",
    isUnilateral: false,
    isWeightPending: false,
  },
});

const mockExercises: ExerciseDto[] = [
  makeLinearExercise("e1", "Squat", 1, 1, 140),
  makeLinearExercise("e2", "Bench Press", 2, 1, 100),
  makeLinearExercise("e3", "Deadlift", 3, 1, 180),
  makeLinearExercise("e4", "Overhead Press", 4, 1, 60),
  makeRpsExercise("e5", "Dumbbell Row", 1, 2, 30),
  makeRpsExercise("e6", "Lateral Raise", 2, 2, 10),
  makeRpsExercise("e7", "Cable Fly", 3, 2, 15),
  makeRpsExercise("e8", "Face Pull", 4, 2, 20),
];

const mockWorkout: WorkoutDto = {
  id: "w-1",
  name: "Hypertrophy Block A",
  variant: 4,
  status: 2,
  currentWeek: 5,
  currentBlock: 1,
  currentDay: 2,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [1],
  isWeekComplete: false,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  startDate: "2026-03-01T00:00:00Z",
  createdAt: "2026-03-01T00:00:00Z",
  startedAt: "2026-03-01T00:00:00Z",
  exerciseCount: 8,
  exercises: mockExercises,
};

const meta = {
  title: "Features/Workout/WorkoutDashboard",
  component: WorkoutDashboard,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();
      return (
        <ClerkProvider publishableKey="pk_test_placeholder">
          <MemoryRouter initialEntries={["/workout"]}>
            <QueryClientProvider client={queryClient}>
              <Story />
            </QueryClientProvider>
          </MemoryRouter>
        </ClerkProvider>
      );
    },
  ],
} satisfies Meta<typeof WorkoutDashboard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ActiveWorkout: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => mockWorkout;
      return <Story />;
    },
  ],
};

export const NoWorkout: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => null as unknown as WorkoutDto;
      return <Story />;
    },
  ],
};

export const Loading: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = () => new Promise(() => {});
      return <Story />;
    },
  ],
};

export const ErrorState: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => {
        throw new Error("Failed to load workout");
      };
      return <Story />;
    },
  ],
};

export const DeloadWeek: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockWorkout,
        currentWeek: 7,
        currentBlock: 1,
        currentDay: 1,
        completedDaysInCurrentWeek: [],
      });
      return <Story />;
    },
  ],
};

export const FinalWeek: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockWorkout,
        currentWeek: 21,
        currentBlock: 3,
        currentDay: 3,
        completedDaysInCurrentWeek: [1, 2],
      });
      return <Story />;
    },
  ],
};

export const AllDaysComplete: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockWorkout,
        completedDaysInCurrentWeek: [1, 2, 3, 4],
        isWeekComplete: true,
      });
      return <Story />;
    },
  ],
};

export const WithMinimalSetsExercise: Story = {
  decorators: [
    (Story) => {
      const minimalSetsExercise: ExerciseDto = {
        id: "e-ms",
        name: "Leg Extension",
        category: ExerciseCategory.Accessory,
        equipment: EquipmentType.Machine,
        assignedDay: 1,
        orderInDay: 3,
        hevyExerciseTemplateId: "leg-ext-id",
        progression: {
          type: "MinimalSets",
          currentWeight: 40,
          weightUnit: "Kilograms",
          targetTotalReps: 30,
          currentSetCount: 3,
          minimumSets: 2,
          maximumSets: 5,
        },
      };
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockWorkout,
        exercises: [...mockExercises, minimalSetsExercise],
        exerciseCount: 9,
      });
      return <Story />;
    },
  ],
};
