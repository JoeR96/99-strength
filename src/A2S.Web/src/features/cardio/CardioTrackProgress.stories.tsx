import type { Meta, StoryObj } from '@storybook/react';
import { CardioTrackChart } from './CardioTrackProgress';
import { phoneParameters } from '@/mocks/storyProviders';
import { stairDetailFixture, walkTrack } from '@/mocks/fixtures/cardioTracks';

const meta = {
  title: 'Features/Cardio/CardioTrackChart',
  component: CardioTrackChart,
  parameters: phoneParameters,
} satisfies Meta<typeof CardioTrackChart>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Stair finisher: two steps up, one hold, with the reason for each. */
export const StairFinisher: Story = { args: { detail: stairDetailFixture } };

/** A new track: no chart yet. */
export const NoSessions: Story = { args: { detail: { track: walkTrack, sessions: [], audit: [], benchmarks: [], chart: [] } } };
