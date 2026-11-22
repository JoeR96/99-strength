import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { TrackLogSheet } from './TrackLogSheet';
import { phoneParameters } from '@/mocks/storyProviders';
import { stairTrack, walkTrack } from '@/mocks/fixtures/cardioTracks';

const meta = {
  title: 'Features/Cardio/TrackLogSheet',
  component: TrackLogSheet,
  parameters: phoneParameters,
  args: { onClose: fn(), onSubmit: fn() },
} satisfies Meta<typeof TrackLogSheet>;

export default meta;
type Story = StoryObj<typeof meta>;

/** One tap: Easy, Solid or Brutal. */
export const Walk: Story = { args: { track: walkTrack } };

/** The optional fields open: level, floors, HR, zone minutes, exact RPE and the pain flag. */
export const StairsWithDetails: Story = { args: { track: stairTrack, initiallyExpanded: true } };
