import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { DraftRestorePrompt } from './DraftRestorePrompt';

const meta = {
  title: 'Shared/DraftRestorePrompt',
  component: DraftRestorePrompt,
  args: { onRestore: fn(), onDiscard: fn(), now: new Date('2026-11-22T21:00:00.000Z') },
  decorators: [
    (Story) => (
      <div className="max-w-sm bg-background p-4">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof DraftRestorePrompt>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Saved earlier today. */
export const SavedToday: Story = { args: { savedAt: '2026-11-22T19:30:00.000Z' } };

/** Saved on an earlier day. */
export const SavedEarlier: Story = { args: { savedAt: '2026-11-18T07:05:00.000Z' } };
