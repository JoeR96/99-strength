import type { Meta, StoryObj } from '@storybook/react';
import { HabitsPage } from './HabitsPage';
import { HabitEditSheet } from './HabitEditSheet';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { habitDefinitions } from '@/mocks/fixtures/habits';

const meta = {
  title: 'Features/Habits/HabitsPage',
  component: HabitsPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/habits')],
} satisfies Meta<typeof HabitsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Five habits with twelve weeks of history. */
export const Default: Story = {
  decorators: [withApiMocks(() => installHabitsHandlers(habitsHandlers.default()))],
};

export const Empty: Story = {
  decorators: [withApiMocks(() => installHabitsHandlers(habitsHandlers.empty()))],
};

/** The edit sheet open on Water. */
export const Editing: Story = {
  decorators: [withApiMocks(() => installHabitsHandlers(habitsHandlers.default()))],
  render: () => (
    <>
      <HabitsPage />
      <HabitEditSheet open habit={habitDefinitions[0]} onClose={() => {}} onSave={() => {}} onArchive={() => {}} />
    </>
  ),
};

export const Loading: Story = {
  decorators: [withApiMocks(() => installHabitsHandlers(habitsHandlers.loading()))],
};

export const Error: Story = {
  decorators: [withApiMocks(() => installHabitsHandlers(habitsHandlers.error()))],
};
