import type { Meta, StoryObj } from '@storybook/react';
import { TracksPage } from './TracksPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { cardioTracksHandlers, installCardioTracksHandlers } from '@/mocks/handlers/cardioTracks';

const meta = {
  title: 'Features/Cardio/TracksPage',
  component: TracksPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/cardio-tracks')],
} satisfies Meta<typeof TracksPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Walk builder one session from a step, walk → run asking to repeat the week, stair finisher. */
export const Default: Story = { decorators: [withApiMocks(() => installCardioTracksHandlers(cardioTracksHandlers.default()))] };
export const Empty: Story = { decorators: [withApiMocks(() => installCardioTracksHandlers(cardioTracksHandlers.empty()))] };
export const Loading: Story = { decorators: [withApiMocks(() => installCardioTracksHandlers(cardioTracksHandlers.loading()))] };
export const Error: Story = { decorators: [withApiMocks(() => installCardioTracksHandlers(cardioTracksHandlers.error()))] };
