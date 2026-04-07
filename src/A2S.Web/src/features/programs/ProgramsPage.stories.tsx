import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ClerkProvider } from "@clerk/clerk-react";
import { ProgramsPage } from "./ProgramsPage";
import { workoutsApi } from "../../api/workouts";
import type { WorkoutSummaryDto } from "../../types/workout";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

const makeSummary = (
  overrides: Partial<WorkoutSummaryDto> & { id: string; name: string }
): WorkoutSummaryDto => ({
  variant: "4",
  totalWeeks: 21,
  currentWeek: 1,
  currentBlock: 1,
  currentDay: 1,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [],
  isWeekComplete: false,
  blockSequence: [1, 2, 3],
  status: "NotStarted",
  createdAt: "2026-01-15T00:00:00Z",
  exerciseCount: 8,
  isActive: false,
  ...overrides,
});

const mockPrograms: WorkoutSummaryDto[] = [
  makeSummary({
    id: "w-1",
    name: "Hypertrophy Block A",
    status: "Active",
    isActive: true,
    currentWeek: 12,
    currentBlock: 2,
    currentDay: 3,
    completedDaysInCurrentWeek: [1, 2],
    startedAt: "2026-01-20T00:00:00Z",
  }),
  makeSummary({
    id: "w-2",
    name: "Strength Phase",
    variant: "5",
    status: "Completed",
    currentWeek: 21,
    totalWeeks: 21,
    exerciseCount: 10,
    createdAt: "2025-09-01T00:00:00Z",
    startedAt: "2025-09-05T00:00:00Z",
    completedAt: "2026-01-10T00:00:00Z",
  }),
  makeSummary({
    id: "w-3",
    name: "Peaking Program",
    variant: "6",
    status: "NotStarted",
    exerciseCount: 12,
    createdAt: "2026-03-20T00:00:00Z",
  }),
];

const meta = {
  title: "Features/Programs/ProgramsPage",
  component: ProgramsPage,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();
      return (
        <ClerkProvider publishableKey="pk_test_placeholder">
          <MemoryRouter initialEntries={["/programs"]}>
            <QueryClientProvider client={queryClient}>
              <Story />
            </QueryClientProvider>
          </MemoryRouter>
        </ClerkProvider>
      );
    },
  ],
} satisfies Meta<typeof ProgramsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const MultiplePrograms: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = async () => mockPrograms;
      return <Story />;
    },
  ],
};

export const SingleActiveProgram: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = async () => [mockPrograms[0]];
      return <Story />;
    },
  ],
};

export const NoPrograms: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = async () => [];
      return <Story />;
    },
  ],
};

export const Loading: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = () => new Promise(() => {});
      return <Story />;
    },
  ],
};

export const ErrorState: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = async () => {
        throw new Error("Network error");
      };
      return <Story />;
    },
  ],
};

export const AllCompleted: Story = {
  decorators: [
    (Story) => {
      workoutsApi.getAllWorkouts = async () => [
        makeSummary({
          id: "w-c1",
          name: "Block A",
          status: "Completed",
          currentWeek: 21,
          totalWeeks: 21,
          completedAt: "2025-12-01T00:00:00Z",
          startedAt: "2025-08-01T00:00:00Z",
        }),
        makeSummary({
          id: "w-c2",
          name: "Block B",
          status: "Completed",
          currentWeek: 21,
          totalWeeks: 21,
          completedAt: "2026-03-15T00:00:00Z",
          startedAt: "2025-12-05T00:00:00Z",
        }),
      ];
      return <Story />;
    },
  ],
};
