import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { SetupWizard } from './SetupWizard';

/**
 * /setup — the program setup wizard, one story per step. Later steps click through
 * the earlier ones with a play function (template route: 4-Day Hypertrophy).
 */
const meta = {
  title: 'Pages/Setup Wizard',
  component: SetupWizard,
  parameters: { route: { path: '/setup' } },
} satisfies Meta<typeof SetupWizard>;

export default meta;
type Story = StoryObj<typeof meta>;

async function next(canvas: ReturnType<typeof within>) {
  await userEvent.click(canvas.getByRole('button', { name: /^Next/ }));
}

export const Welcome: Story = {};

export const ChooseTemplate: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('button', { name: /Start from Template/ }));
    await next(canvas);
    await userEvent.click(await canvas.findByRole('button', { name: /4-Day Hypertrophy/ }));
  },
};

export const Exercises: Story = {
  play: async (context) => {
    await ChooseTemplate.play!(context);
    await next(within(context.canvasElement));
  },
};

export const Review: Story = {
  play: async (context) => {
    await Exercises.play!(context);
    await next(within(context.canvasElement));
  },
};
