import type { Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { phoneParameters, withApiMocks, withAppProviders } from '@/mocks/storyProviders';
import { installPlanTemplatesHandlers, planTemplatesHandlers } from '@/mocks/handlers/planTemplates';
import { PlanTemplatesPage } from './PlanTemplatesPage';

function Routed(props: { initialApplyOpen?: boolean; initialReplaceName?: string | null }) {
  return (
    <Routes>
      <Route path="/plans/templates" element={<PlanTemplatesPage {...props} />} />
      <Route path="/plans/templates/:key" element={<PlanTemplatesPage {...props} />} />
    </Routes>
  );
}

const meta = {
  title: 'Features/Plans/PlanTemplatesPage',
  component: Routed,
  parameters: phoneParameters,
} satisfies Meta<typeof Routed>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Every template: Plan v3 and Lumbridge Lard Shed. */
export const List: Story = {
  decorators: [withApiMocks(() => installPlanTemplatesHandlers(planTemplatesHandlers.default())), withAppProviders('/plans/templates')],
};

/** Plan v3 previewed: phases with dates, the week, Upper A and Lower A with slots 2a/2b, routines, tracks, what's left to fill in. */
export const Preview: Story = {
  decorators: [withApiMocks(() => installPlanTemplatesHandlers(planTemplatesHandlers.default())), withAppProviders('/plans/templates/plan-v3')],
};

/** The apply dialog: start date snapped to Monday, optional training maxes, start weight. */
export const ApplyDialog: Story = {
  args: { initialApplyOpen: true },
  decorators: [withApiMocks(() => installPlanTemplatesHandlers(planTemplatesHandlers.needsReplace())), withAppProviders('/plans/templates/plan-v3')],
};

/** Another plan is active: replace it explicitly (its history is kept). */
export const ApplyReplaceConfirm: Story = {
  args: { initialApplyOpen: true, initialReplaceName: 'Lumbridge Lard Shed' },
  decorators: [withApiMocks(() => installPlanTemplatesHandlers(planTemplatesHandlers.needsReplace())), withAppProviders('/plans/templates/plan-v3')],
};
