import type { Meta, StoryObj } from '@storybook/react';
import { MemoryRouter } from 'react-router-dom';
import { SessionCard } from './SessionCard';
import { HEVY_ROUTINE_ID, todayCardio, todayDone, todayUpperA } from '@/mocks/fixtures/plan';

const DATE = '2026-11-09';
const upperA = todayUpperA(DATE).sessions[0];
const fourByFour = todayCardio(DATE).sessions[0];

const meta = {
  title: 'Features/Today/SessionCard',
  component: SessionCard,
  parameters: { layout: 'padded', viewport: { defaultViewport: 'mobile2' } },
  decorators: [
    (Story) => (
      <MemoryRouter>
        <div className="mx-auto max-w-sm">
          <Story />
        </div>
      </MemoryRouter>
    ),
  ],
  args: { session: upperA },
} satisfies Meta<typeof SessionCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The session was pushed, so Open in Hevy sits beside Start. */
export const WithHevyRoutine: Story = {
  args: { session: { ...upperA, hevyRoutineId: HEVY_ROUTINE_ID }, hevyConfigured: true },
};

/** A key is stored and the session has no routine yet: Push to Hevy beside Start. */
export const PushAvailable: Story = {
  args: { hevyConfigured: true },
};

/** No Hevy key: Start on its own, full width. */
export const NoKey: Story = {
  args: { hevyConfigured: false },
};

/** The push is in flight. */
export const Pushing: Story = {
  args: { hevyConfigured: true, pushing: true },
};

/** Cardio gets the same pair: Mark done and Push to Hevy. */
export const CardioPushAvailable: Story = {
  args: { session: fourByFour, hevyConfigured: true },
};

/** Once done there is nothing to push or open, only Undo. */
export const DoneWithRoutine: Story = {
  args: {
    session: { ...todayDone(DATE).sessions[0], hevyRoutineId: HEVY_ROUTINE_ID },
    hevyConfigured: true,
    onUndo: () => {},
  },
};
