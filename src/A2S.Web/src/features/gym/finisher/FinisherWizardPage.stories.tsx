import type { Meta, StoryObj } from '@storybook/react';
import { phoneParameters } from '@/mocks/storyProviders';
import { buildFinisherPlan } from './finisherPlan';
import { pause, skip, startEngine } from './finisherEngine';
import { FinisherWizard } from './FinisherWizard';

/** The live stair-finisher wizard on a frozen clock: 3 min warm-up @ L3, 7 × 1:1 @ L6, 3 min cool-down. */
const T0 = Date.parse('2026-11-23T18:00:00.000Z');
const phases = buildFinisherPlan({
  warmUp: { minutes: 3, level: 3, zone: 'Z2' },
  coolDown: { minutes: 3, level: 3, zone: 'Z2' },
  workMinutes: 14,
  level: 6,
  intervals: { rounds: 7, workSeconds: 60, restSeconds: 60 },
});
const total = phases.reduce((a, p) => a + p.seconds, 0);
const started = startEngine('story-session', phases, T0);
const at = (seconds: number) => () => T0 + seconds * 1000;

const meta = {
  title: 'Features/Gym/FinisherWizardPage',
  component: FinisherWizard,
  parameters: phoneParameters,
  args: {
    name: 'Stair finisher',
    clientSessionId: 'story-session',
    phases,
    initial: started,
    clock: at(40),
    initialSpeak: false,
    onDone: () => {},
    onAbandon: () => {},
    onSchedule: () => {},
  },
} satisfies Meta<typeof FinisherWizard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** 40 s into the 3 min warm-up: level 3 in zone 2, next up the first hard minute. */
export const WarmUp: Story = {};

/** Round 4 of 7, 25 s into the hard minute at level 6. */
export const HardInterval: Story = { args: { clock: at(180 + 120 * 3 + 25) } };

/** Paused during the first hard minute (after skipping the warm-up). */
export const Paused: Story = { args: { initial: pause(skip(started, T0 + 60_000), T0 + 80_000), clock: at(600) } };

/** All phases done: the summary that prefills the finisher log. */
export const FinishedSummary: Story = { args: { clock: at(total + 10) } };
