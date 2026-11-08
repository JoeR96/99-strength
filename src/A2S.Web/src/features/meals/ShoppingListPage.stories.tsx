import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ClerkProvider } from '@clerk/clerk-react';
import { ShoppingListPage } from './ShoppingListPage';
import { mealsApi } from '@/api/meals';
import { WEEK, emptyList, generatedList, generatedListText, orzo } from './mealsFixtures';
import type { ShoppingListDto } from '@/types/meals';

const createQueryClient = () =>
  new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });

const meta = {
  title: 'Features/Meals/ShoppingListPage',
  component: ShoppingListPage,
  parameters: { layout: 'fullscreen' },
  decorators: [
    (Story) => (
      <ClerkProvider publishableKey="pk_test_placeholder">
        <MemoryRouter initialEntries={[`/shopping-list?week=${WEEK}`]}>
          <QueryClientProvider client={createQueryClient()}>
            <Story />
          </QueryClientProvider>
        </MemoryRouter>
      </ClerkProvider>
    ),
  ],
} satisfies Meta<typeof ShoppingListPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Ticks in memory so the story behaves like the real page. */
function withTogglingList(initial: ShoppingListDto) {
  let current = initial;
  mealsApi.getShoppingList = async () => current;
  mealsApi.toggleShoppingItem = async (_listId, name, unit) => {
    const items = current.items.map((i) => (i.name === name && i.unit === unit ? { ...i, checked: !i.checked } : i));
    current = { ...current, items, checkedCount: items.filter((i) => i.checked).length };
    return current;
  };
  mealsApi.generateShoppingList = async () => current;
  mealsApi.exportShoppingList = async () => generatedListText;
}

export const OneOfFiveTicked: Story = {
  decorators: [
    (Story) => {
      withTogglingList(generatedList);
      return <Story />;
    },
  ],
};

export const AllTicked: Story = {
  decorators: [
    (Story) => {
      withTogglingList({
        ...generatedList,
        items: generatedList.items.map((i) => ({ ...i, checked: true })),
        checkedCount: generatedList.items.length,
      });
      return <Story />;
    },
  ],
};

export const NoListYet: Story = {
  decorators: [
    (Story) => {
      withTogglingList(emptyList);
      return <Story />;
    },
  ],
};

export const MealsPickedButNotGenerated: Story = {
  decorators: [
    (Story) => {
      withTogglingList({ ...emptyList, id: generatedList.id, sourceMealIds: [orzo.id] });
      mealsApi.generateShoppingList = async () => generatedList;
      return <Story />;
    },
  ],
};

export const Loading: Story = {
  decorators: [
    (Story) => {
      mealsApi.getShoppingList = () => new Promise<ShoppingListDto>(() => {});
      return <Story />;
    },
  ],
};
