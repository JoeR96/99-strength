import type { Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { RoutinePlayerPage } from './RoutinePlayerPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { MOBILITY_B_ID } from '@/mocks/fixtures/plan';

const meta = {
  title: 'Features/Routines/RoutinePlayerPage',
  component: RoutinePlayerPage,
  parameters: phoneParameters,
} satisfies Meta<typeof RoutinePlayerPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The start screen before Start is tapped. */
export const Ready: Story = {
  decorators: [
    withApiMocks(() => {
      installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default());
      installPlansHandlers(plansHandlers.cardio());
    }),
    withAppProviders(`/routines/${MOBILITY_B_ID}/play`),
  ],
  render: () => (
    <Routes>
      <Route path="/routines/:id/play" element={<RoutinePlayerPage />} />
    </Routes>
  ),
};
