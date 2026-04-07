import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { SetupWizard } from "./SetupWizard";
import { workoutsApi } from "../../api/workouts";
import {
  EquipmentType,
  type ExerciseTemplate,
} from "../../types/workout";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

const mockTemplates: ExerciseTemplate[] = [
  {
    name: "Squat",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Back Squat",
  },
  {
    name: "Bench Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Barbell Bench Press",
  },
  {
    name: "Deadlift",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 1, target: 3, maximum: 5 },
    defaultSets: 3,
    description: "Conventional Deadlift",
  },
  {
    name: "Overhead Press",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: "Standing Overhead Press",
  },
  {
    name: "Barbell Row",
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 10, maximum: 12 },
    defaultSets: 4,
    description: "Bent-over barbell row",
  },
  {
    name: "Dumbbell Curl",
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 10, target: 15, maximum: 20 },
    defaultSets: 3,
    description: "Bicep isolation",
  },
  {
    name: "Leg Press",
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 8, target: 12, maximum: 15 },
    defaultSets: 3,
    description: "Leg press machine",
  },
  {
    name: "Pull-up",
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 5, target: 10, maximum: 15 },
    defaultSets: 3,
    description: "Bodyweight vertical pull",
  },
];

if (typeof window !== "undefined") {
  workoutsApi.getExerciseLibrary = async () => ({
    templates: mockTemplates,
  });
  workoutsApi.createWorkout = async () => ({
    id: "mock-workout-id",
    name: "Mock Workout",
    variant: 4,
    status: 2,
    currentWeek: 1,
    currentBlock: 1,
    currentDay: 1,
    daysPerWeek: 4,
    completedDaysInCurrentWeek: [],
    isWeekComplete: false,
    totalWeeks: 21,
    blockSequence: [1, 2, 3],
    startDate: new Date().toISOString(),
    createdAt: new Date().toISOString(),
    exerciseCount: 0,
    exercises: [],
  });
}

const meta = {
  title: "Features/Workout/SetupWizard",
  component: SetupWizard,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();
      return (
        <MemoryRouter initialEntries={["/setup"]}>
          <QueryClientProvider client={queryClient}>
            <Story />
          </QueryClientProvider>
        </MemoryRouter>
      );
    },
  ],
} satisfies Meta<typeof SetupWizard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WelcomeStep: Story = {};

export const WelcomeWithTemplateSelected: Story = {
  play: async ({ canvasElement }) => {
    const buttons = canvasElement.querySelectorAll("button");
    const templateButton = Array.from(buttons).find((b) =>
      b.textContent?.includes("Start from Template")
    );
    templateButton?.click();
  },
};

export const WelcomeWithScratchSelected: Story = {
  play: async ({ canvasElement }) => {
    const buttons = canvasElement.querySelectorAll("button");
    const scratchButton = Array.from(buttons).find((b) =>
      b.textContent?.includes("Build from Scratch")
    );
    scratchButton?.click();
  },
};

export const TemplateSelectionStep: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const buttons = canvasElement.querySelectorAll("button");
    const templateButton = Array.from(buttons).find((b) =>
      b.textContent?.includes("Start from Template")
    );
    templateButton?.click();

    await new Promise((r) => setTimeout(r, 100));
    const nextButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent?.includes("Next")
    );
    nextButton?.click();
  },
};

export const ExerciseConfigStep: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const scratchButton = Array.from(
      canvasElement.querySelectorAll("button")
    ).find((b) => b.textContent?.includes("Build from Scratch"));
    scratchButton?.click();

    await new Promise((r) => setTimeout(r, 100));
    const nextButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent?.includes("Next")
    );
    nextButton?.click();
  },
};

export const ConfirmationStepEmpty: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const scratchButton = Array.from(
      canvasElement.querySelectorAll("button")
    ).find((b) => b.textContent?.includes("Build from Scratch"));
    scratchButton?.click();

    await new Promise((r) => setTimeout(r, 100));
    let nextButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent?.includes("Next")
    );
    nextButton?.click();

    await new Promise((r) => setTimeout(r, 300));
    nextButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent?.includes("Next")
    );
    nextButton?.click();
  },
};
