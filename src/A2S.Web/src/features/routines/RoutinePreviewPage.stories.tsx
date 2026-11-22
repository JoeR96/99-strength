import type { Decorator, Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { RoutinePreviewPage } from './RoutinePreviewPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { MOBILITY_B_ID, MOBILITY_YOGA_ID } from '@/mocks/fixtures/plan';

const routed: Decorator = (Story) => (
  <Routes>
    <Route path="/routines/:id" element={<Story />} />
  </Routes>
);

function at(id: string): Decorator[] {
  return [
    routed,
    withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.home())),
    withAppProviders(`/routines/${id}`),
  ];
}

const meta = {
  title: 'Features/Routines/RoutinePreviewPage',
  component: RoutinePreviewPage,
  parameters: phoneParameters,
} satisfies Meta<typeof RoutinePreviewPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Home yoga: every pose with its dose and cues, the current level and the Easier / Harder versions. */
export const YogaWithLevels: Story = { decorators: at(MOBILITY_YOGA_ID) };

/** Mobility B: plain drills with cues and the standing alternative. */
export const Stretch: Story = { decorators: at(MOBILITY_B_ID) };
