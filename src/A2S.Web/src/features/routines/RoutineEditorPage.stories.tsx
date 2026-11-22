import type { Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { RoutineEditorPage } from './RoutineEditorPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { MOBILITY_A_ID, MOBILITY_VIDEO_ID, MOBILITY_YOGA_ID } from '@/mocks/fixtures/plan';
import { emptyRoutine } from './routineForm';

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

/** Reopened after leaving with unsaved changes: "Restore unsaved changes?" above the form. */
export const WithUnsavedDraft: Story = {
  decorators: [
    withApiMocks(() => {
      installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default());
      const base = emptyRoutine();
      const value = { ...base, name: 'Evening reset', targetMinutes: '12', drills: base.drills.map((d) => ({ ...d, key: '', name: 'Cat-cow' })) };
      try {
        localStorage.setItem('a2s-draft:routine:new', JSON.stringify({ version: 2, savedAt: '2026-11-22T19:30:00.000Z', value }));
      } catch {
        /* storage unavailable in this preview */
      }
    }),
    withAppProviders('/routines/new'),
  ],
  render: routes,
};

/** A yoga flow whose drills have levels: the reverse plank ladder (Level 1-3, 15 to 30 s) and Easier / Standard / Harder. */
export const EditingLevels: Story = {
  decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.home())), withAppProviders(`/routines/${MOBILITY_YOGA_ID}/edit`)],
  render: routes,
};

/** A follow-along video: kind, link and minutes, no drills. */
export const EditingFollowAlong: Story = {
  decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.home())), withAppProviders(`/routines/${MOBILITY_VIDEO_ID}/edit`)],
  render: routes,
};
