import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { CurrentWorkoutPage } from './CurrentWorkoutPage';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installWorkoutsHandlers, workoutsHandlers } from '@/mocks/handlers/workouts';
import { installSuggestedPlansHandlers, suggestedPlansHandlers } from '@/mocks/handlers/suggestedPlans';
import { installMobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { HevyStatusStub } from '@/mocks/HevyStatusStub';
import { todayDoneNotInHevy } from '@/mocks/fixtures/plan';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'Joe' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  UserButton: () => <div data-testid="user-button" />,
  SignedIn: ({ children }: { children: ReactNode }) => <>{children}</>,
  SignedOut: () => null,
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

function renderPage(hevyKey = false) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <HevyStatusStub isConfigured={hevyKey}>
        <MemoryRouter initialEntries={['/today']}>
          <CurrentWorkoutPage />
        </MemoryRouter>
      </HevyStatusStub>
    </QueryClientProvider>
  );
}

describe('CurrentWorkoutPage', () => {
  beforeEach(() => {
    installPlansHandlers(plansHandlers.default());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    installWorkoutsHandlers(workoutsHandlers.cycle());
    installMobilityRoutinesHandlers();
  });

  it('shows the current session of the cycle with its exercises, the cardio finisher and Start', async () => {
    renderPage();

    const hero = await screen.findByTestId('current-session');
    expect(screen.getByRole('heading', { level: 1, name: 'Current workout' })).toBeInTheDocument();
    expect(within(hero).getByTestId('cycle-position')).toHaveTextContent('Week 1 · Session 1 of 4');
    expect(within(hero).getByRole('heading', { name: 'Upper A' })).toBeInTheDocument();
    expect(hero).toHaveTextContent('Bench Press (Barbell)');
    expect(hero).toHaveTextContent('4 × 5, 1 × 10+ · 75 kg');
    expect(within(hero).getByTestId('planned-cardio')).toHaveTextContent('Stair finisher5 min continuous @ level 6');
    expect(hero).toHaveTextContent('2 more sessions to step up');
    expect(screen.getByTestId('start-workout')).toHaveAttribute('href', '/gym/1');
    expect(screen.queryByText(/rest day/i)).not.toBeInTheDocument();
  });

  it('mid-cycle: the current session is the next one to do, and the rest of the cycle previews by week and day', async () => {
    installWorkoutsHandlers(workoutsHandlers.cycle({ week: 3, day: 2 }));
    renderPage();

    expect(await screen.findByTestId('cycle-position')).toHaveTextContent('Week 3 · Session 2 of 4');
    expect(screen.getByRole('heading', { name: 'Lower A' })).toBeInTheDocument();
    expect(screen.getByTestId('start-workout')).toHaveAttribute('href', '/gym/2');
    expect(screen.getByTestId('cycle-row-3-3')).toHaveAttribute('href', '/gym/3?preview=1&week=3');
    expect(screen.getByTestId('cycle-row-3-4')).toBeInTheDocument();
    expect(screen.queryByTestId('cycle-row-4-1')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('preview-next-week'));

    expect(screen.getByTestId('cycle-row-4-1')).toHaveAttribute('href', '/gym/1?preview=1&week=4');
  });

  it('marks a deload week', async () => {
    installWorkoutsHandlers(workoutsHandlers.cycle({ week: 7, day: 1, deload: true }));
    renderPage();

    expect(await screen.findByTestId('cycle-position')).toHaveTextContent('Week 7 · Session 1 of 4Deload');
    expect(screen.getByTestId('current-session')).toHaveTextContent('4 × 5 · 75 kg');
  });

  it('a lift with no training max offers Set TM inline instead of weights', async () => {
    installWorkoutsHandlers(workoutsHandlers.cycle({ pendingTm: true }));
    renderPage();

    expect(await screen.findByRole('button', { name: 'Set training max for Bench Press (Barbell)' })).toBeInTheDocument();
    expect(screen.getByTestId('current-session')).not.toHaveTextContent('75 kg');
  });

  it('just finished: the last session says Not in Hevy yet with Retry, and the next session is already shown', async () => {
    installPlansHandlers({ ...plansHandlers.default(), getToday: async (date) => todayDoneNotInHevy(date ?? '2026-11-09') });
    installWorkoutsHandlers(workoutsHandlers.cycle({ day: 2 }));
    renderPage(true);

    expect(await screen.findByTestId('not-in-hevy')).toBeInTheDocument();
    expect(screen.getByTestId('retry-hevy')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Lower A' })).toBeInTheDocument();
  });

  it('links the paired home routine with Play', async () => {
    renderPage();

    const paired = await screen.findByTestId('paired-routine');
    expect(paired).toHaveTextContent('After this workoutMobility B');
    expect(screen.getByTestId('warm-up-count')).toHaveTextContent('Warm-up: 2 drills');
    expect(within(paired).getByRole('link', { name: /^Play / })).toHaveAttribute('href', expect.stringMatching(/^\/routines\/.+\/play$/));
  });

  it('with no active workout shows the plan picker', async () => {
    installWorkoutsHandlers(workoutsHandlers.none());
    installSuggestedPlansHandlers(suggestedPlansHandlers.default());
    renderPage();

    expect(await screen.findByRole('link', { name: 'Browse plan templates' })).toHaveAttribute('href', '/plans/templates');
    expect(screen.queryByTestId('current-session')).not.toBeInTheDocument();
  });
});
