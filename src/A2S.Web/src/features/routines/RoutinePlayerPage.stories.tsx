import type { Meta, StoryObj } from '@storybook/react';
import { Route, Routes } from 'react-router-dom';
import { RoutinePlayerPage } from './RoutinePlayerPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { MOBILITY_B_ID, MOBILITY_VIDEO_ID, MOBILITY_YOGA_ID } from '@/mocks/fixtures/plan';
import { followAlongFixture } from '@/mocks/fixtures/mobility';
import { FollowAlongPlayer } from './FollowAlongPlayer';

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

const playerRoutes = () => (
  <Routes>
    <Route path="/routines/:id/play" element={<RoutinePlayerPage />} />
  </Routes>
);

const homeMocks = (levelUp = false) =>
  withApiMocks(() => {
    installMobilityRoutinesHandlers(levelUp ? mobilityRoutinesHandlers.homeLevelUp() : mobilityRoutinesHandlers.home());
    installPlansHandlers(plansHandlers.homeYoga());
  });

async function waitFor<T>(find: () => T | null | undefined, ms = 8000): Promise<T | null> {
  const deadline = Date.now() + ms;
  for (;;) {
    const found = find();
    if (found || Date.now() > deadline) return found ?? null;
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

const byTestId = (root: HTMLElement, id: string) => root.querySelector<HTMLButtonElement>(`[data-testid="${id}"]`);
const byLabel = (root: HTMLElement, label: string) => root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
const byText = (root: HTMLElement, text: string) =>
  Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.trim() === text) ?? null;

/** Mid-routine on a drill with levels: the level name and cues, "Left side first", and the easier version opened. Paused. */
export const WithLevels: Story = {
  decorators: [homeMocks(), withAppProviders(`/routines/${MOBILITY_YOGA_ID}/play`)],
  render: playerRoutes,
  play: async ({ canvasElement }) => {
    (await waitFor(() => byTestId(canvasElement, 'start-routine')))?.click();
    for (let i = 0; i < 3; i++) (await waitFor(() => byLabel(canvasElement, 'Next step')))?.click();
    (await waitFor(() => byTestId(canvasElement, 'pause-resume')))?.click();
    (await waitFor(() => byText(canvasElement, 'Too much? Easier version')))?.click();
  },
};

async function finishLadderAndSave(canvasElement: HTMLElement) {
  (await waitFor(() => byTestId(canvasElement, 'start-routine')))?.click();
  // Skip the get-ready gap, so the first ladder hold is running, then finish it and skip the rest.
  (await waitFor(() => byLabel(canvasElement, 'Next step')))?.click();
  await waitFor(() => (canvasElement.querySelector('[data-testid="player-clock"]')?.getAttribute('aria-label')?.endsWith('seconds') && canvasElement.textContent?.includes('Go') ? true : null));
  for (let i = 0; i < 6 && !byTestId(canvasElement, 'save-feedback'); i++) {
    byLabel(canvasElement, 'Next step')?.click();
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
  (await waitFor(() => byTestId(canvasElement, 'save-feedback')))?.click();
  await waitFor(() => byTestId(canvasElement, 'next-time') as HTMLButtonElement | null);
}

/** Finished after a second clean session on the ladder: "Next time: Reverse plank ladder — 3 × 30 s". */
export const LadderStepUp: Story = {
  decorators: [homeMocks(), withAppProviders(`/routines/${MOBILITY_YOGA_ID}/play`)],
  render: playerRoutes,
  play: async ({ canvasElement }) => finishLadderAndSave(canvasElement),
};

/** At the top of Level 1: "Next time: Level 2: Hands on a bench, straight legs" with Accept / Not yet. */
export const LadderLevelUp: Story = {
  decorators: [homeMocks(true), withAppProviders(`/routines/${MOBILITY_YOGA_ID}/play`)],
  render: playerRoutes,
  play: async ({ canvasElement }) => finishLadderAndSave(canvasElement),
};

/** A follow-along video before Start (Start opens the link in a new tab). */
export const FollowAlong: Story = {
  decorators: [homeMocks(), withAppProviders(`/routines/${MOBILITY_VIDEO_ID}/play`)],
  render: playerRoutes,
};

/** Back from the video: "I'm back — log it" with the expected minutes filled in. */
export const FollowAlongLogIt: Story = {
  decorators: [homeMocks(), withAppProviders(`/routines/${MOBILITY_VIDEO_ID}/play`)],
  render: () => <FollowAlongPlayer routine={followAlongFixture} initiallyBack />,
};

/** Ready screen with Progress: each drill's level and dose, Level 2 waiting for Accept / Not yet, recent changes and Undo. */
export const ReadyWithProgress: Story = {
  decorators: [
    withApiMocks(() => {
      installMobilityRoutinesHandlers(mobilityRoutinesHandlers.homePending());
      installPlansHandlers(plansHandlers.homeYoga());
    }),
    withAppProviders(`/routines/${MOBILITY_YOGA_ID}/play`),
  ],
  render: playerRoutes,
};
