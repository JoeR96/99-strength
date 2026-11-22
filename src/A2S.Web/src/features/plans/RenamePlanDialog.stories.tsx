import type { Meta, StoryObj } from '@storybook/react';
import { RenamePlanDialog } from './RenamePlanDialog';
import { plansApi } from '@/api/plans';
import { withApiMocks, withAppProviders, phoneParameters } from '@/mocks/storyProviders';
import { installHandlers } from '@/mocks/handlers/install';
import { planFixture } from '@/mocks/fixtures/plan';

const meta = {
  title: 'Features/Plans/RenamePlanDialog',
  component: RenamePlanDialog,
  parameters: phoneParameters,
  decorators: [withAppProviders('/progress')],
  args: { open: true, planId: 'plan-1', currentName: 'Lumbridge Lard Shed', onClose: () => {} },
} satisfies Meta<typeof RenamePlanDialog>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The dialog open on the current name; Save renames and shows a toast. */
export const Open: Story = {
  decorators: [withApiMocks(() => installHandlers(plansApi, { rename: async (_id, name) => ({ ...planFixture, name }) }))],
};
