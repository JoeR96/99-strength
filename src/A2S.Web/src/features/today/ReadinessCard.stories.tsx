import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { ReadinessCard } from './ReadinessCard';
import { phoneParameters } from '@/mocks/storyProviders';
import { readinessFixture } from '@/mocks/handlers/readiness';

const meta = {
  title: 'Features/Today/ReadinessCard',
  component: ReadinessCard,
  parameters: phoneParameters,
  args: { onSelect: fn(), onKeepFinisher: fn() },
} satisfies Meta<typeof ReadinessCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Nothing tapped yet today. */
export const NotSet: Story = { args: { readiness: readinessFixture('2026-11-22', null) } };

/** Green: no advice. */
export const Green: Story = { args: { readiness: readinessFixture('2026-11-22', 'Green') } };

/** Yellow: a gentle note only. */
export const Yellow: Story = { args: { readiness: readinessFixture('2026-11-22', 'Yellow') } };

/** Red: finisher skipped for recovery, with one tap to undo; hard cardio suggested off. */
export const Red: Story = { args: { readiness: readinessFixture('2026-11-22', 'Red') } };

/** Red, finisher kept after tapping Undo. */
export const RedFinisherKept: Story = { args: { readiness: readinessFixture('2026-11-22', 'Red', true) } };
