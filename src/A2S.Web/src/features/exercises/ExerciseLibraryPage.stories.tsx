import type { Meta, StoryObj } from "@storybook/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ClerkProvider } from "@clerk/clerk-react";
import { ExerciseLibraryPage } from "./ExerciseLibraryPage";

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

const meta = {
  title: "Features/Exercises/ExerciseLibraryPage",
  component: ExerciseLibraryPage,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => {
      const queryClient = createQueryClient();
      return (
        <ClerkProvider publishableKey="pk_test_placeholder">
          <MemoryRouter initialEntries={["/exercises"]}>
            <QueryClientProvider client={queryClient}>
              <Story />
            </QueryClientProvider>
          </MemoryRouter>
        </ClerkProvider>
      );
    },
  ],
} satisfies Meta<typeof ExerciseLibraryPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const GridView: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const gridButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent === "Grid"
    );
    gridButton?.click();
  },
};

export const ListView: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const listButton = Array.from(canvasElement.querySelectorAll("button")).find(
      (b) => b.textContent === "List"
    );
    listButton?.click();
  },
};

export const SearchResults: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const input = canvasElement.querySelector(
      'input[placeholder="Search exercises..."]'
    ) as HTMLInputElement | null;
    if (input) {
      const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
        window.HTMLInputElement.prototype,
        "value"
      )?.set;
      nativeInputValueSetter?.call(input, "squat");
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
  },
};

export const EmptySearch: Story = {
  play: async ({ canvasElement }) => {
    await new Promise((r) => setTimeout(r, 100));
    const input = canvasElement.querySelector(
      'input[placeholder="Search exercises..."]'
    ) as HTMLInputElement | null;
    if (input) {
      const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
        window.HTMLInputElement.prototype,
        "value"
      )?.set;
      nativeInputValueSetter?.call(input, "zzzzzznonexistent");
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
  },
};
