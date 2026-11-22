import type { Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { RoutineEditorPage } from './RoutineEditorPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { MOBILITY_A_ID } from '@/mocks/fixtures/plan';

const meta = {
  title: 'Features/Routines/RoutineEditorPage',
  component: RoutineEditorPage,
  parameters: phoneParameters,
} satisfies Meta<typeof RoutineEditorPage>;

export default meta;
type Story = StoryObj<typeof meta>;

const routes = () => (
  <Routes>
    <Route path="/routines/new" element={<RoutineEditorPage />} />
    <Route path="/routines/:id/edit" element={<RoutineEditorPage />} />
  </Routes>
);

export const New: Story = {
  decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default())), withAppProviders('/routines/new')],
  render: routes,
};

export const Editing: Story = {
  decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default())), withAppProviders(`/routines/${MOBILITY_A_ID}/edit`)],
  render: routes,
};
