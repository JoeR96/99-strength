import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ClerkProvider } from '@clerk/clerk-react';
import { MealsPage } from './MealsPage';
import { mealsApi } from '@/api/meals';
import { WEEK, emptyWeek, generatedList, meals, weekWithOrzoAndBarbacoa } from './mealsFixtures';
import type { MealDto } from '@/types/meals';

const createQueryClient = () =>
  new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });

const meta = {
  title: 'Features/Meals/MealsPage',
  component: MealsPage,
  parameters: { layout: 'fullscreen' },
  decorators: [
    (Story) => (
      <ClerkProvider publishableKey="pk_test_placeholder">
        <MemoryRouter initialEntries={[`/meals?week=${WEEK}`]}>
          <QueryClientProvider client={createQueryClient()}>
            <Story />
          </QueryClientProvider>
        </MemoryRouter>
      </ClerkProvider>
    ),
  ],
} satisfies Meta<typeof MealsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const NothingPickedYet: Story = {
  decorators: [
    (Story) => {
      mealsApi.getMeals = async () => meals;
      mealsApi.getWeekMeals = async () => emptyWeek;
      mealsApi.setWeekMeals = async (weekStart, mealIds) => ({ weekStart, mealIds });
      mealsApi.generateShoppingList = async () => generatedList;
      return <Story />;
    },
  ],
};

export const TwoMealsThisWeek: Story = {
  decorators: [
    (Story) => {
      mealsApi.getMeals = async () => meals;
      mealsApi.getWeekMeals = async () => weekWithOrzoAndBarbacoa;
      mealsApi.setWeekMeals = async (weekStart, mealIds) => ({ weekStart, mealIds });
      mealsApi.generateShoppingList = async () => generatedList;
      return <Story />;
    },
  ],
};

export const Loading: Story = {
  decorators: [
    (Story) => {
      mealsApi.getMeals = () => new Promise<MealDto[]>(() => {});
      mealsApi.getWeekMeals = () => new Promise(() => {});
      return <Story />;
    },
  ],
};

export const FailedToLoad: Story = {
  decorators: [
    (Story) => {
      mealsApi.getMeals = async () => {
        throw new Error('boom');
      };
      mealsApi.getWeekMeals = async () => emptyWeek;
      return <Story />;
    },
  ],
};
