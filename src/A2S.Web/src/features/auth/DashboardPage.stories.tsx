import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ClerkProvider } from "@clerk/clerk-react";
import { DashboardPage } from "./DashboardPage";
import { workoutsApi } from "../../api/workouts";
import type { WorkoutDto, ExerciseDto } from "../../types/workout";
import { WeightUnit, ExerciseCategory, EquipmentType } from "../../types/workout";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

const mockExercises: ExerciseDto[] = [
  {
    id: "ex-1",
    name: "Squat",
    category: ExerciseCategory.MainLift,
    equipment: EquipmentType.Barbell,
    assignedDay: 1,
    orderInDay: 1,
    hevyExerciseTemplateId: "squat-1",
    progression: {
      type: "Linear",
      trainingMax: { value: 140, unit: WeightUnit.Kilograms },
      useAmrap: true,
      baseSetsPerExercise: 5,
    },
  },
  {
    id: "ex-2",
    name: "Bench Press",
    category: ExerciseCategory.MainLift,
    equipment: EquipmentType.Barbell,
    assignedDay: 2,
    orderInDay: 1,
    hevyExerciseTemplateId: "bench-1",
    progression: {
      type: "Linear",
      trainingMax: { value: 100, unit: WeightUnit.Kilograms },
      useAmrap: true,
      baseSetsPerExercise: 5,
    },
  },
  {
    id: "ex-3",
    name: "Dumbbell Curl",
    category: ExerciseCategory.Accessory,
    equipment: EquipmentType.Dumbbell,
    assignedDay: 1,
    orderInDay: 2,
    hevyExerciseTemplateId: "curl-1",
    progression: {
      type: "RepsPerSet",
      repRange: { minimum: 10, maximum: 15 },
      startingSets: 3,
      currentSetCount: 4,
      targetSets: 5,
      currentWeight: 14,
      weightUnit: "Kilograms",
      isUnilateral: true,
      isWeightPending: false,
    },
  },
];

const mockActiveWorkout: WorkoutDto = {
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
  exerciseCount: 3,
  exercises: mockExercises,
};

const meta = {
  title: "Features/Dashboard/DashboardPage",
  component: DashboardPage,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();
      return (
        <ClerkProvider publishableKey="pk_test_placeholder">
          <MemoryRouter initialEntries={["/dashboard"]}>
            <QueryClientProvider client={queryClient}>
              <Story />
            </QueryClientProvider>
          </MemoryRouter>
        </ClerkProvider>
      );
    },
  ],
} satisfies Meta<typeof DashboardPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithActiveWorkout: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => mockActiveWorkout;
      return <Story />;
    },
  ],
};

export const NoActiveWorkout: Story = {
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

export const WorkoutNearCompletion: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockActiveWorkout,
        currentWeek: 20,
        currentBlock: 3,
        currentDay: 4,
        completedDaysInCurrentWeek: [1, 2, 3],
      });
      return <Story />;
    },
  ],
};

export const WeekFullyCompleted: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getCurrentWorkout = async () => ({
        ...mockActiveWorkout,
        currentWeek: 5,
        currentDay: 1,
        completedDaysInCurrentWeek: [1, 2, 3, 4],
        isWeekComplete: true,
      });
      return <Story />;
    },
  ],
};
