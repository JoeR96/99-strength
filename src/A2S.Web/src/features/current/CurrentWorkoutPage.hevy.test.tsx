import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { CurrentWorkoutPage } from './CurrentWorkoutPage';
import { workoutsApi } from '@/api/workouts';
import { installWorkoutsHandlers, workoutsHandlers } from '@/mocks/handlers/workouts';
import { hevySyncApi } from '@/api/hevySync';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { pullSummaryFixture } from '@/mocks/fixtures/hevyInbox';
import { todayDone } from '@/mocks/fixtures/plan';
import { HevyStatusStub } from '@/mocks/HevyStatusStub';
import { PULL_TO_REFRESH_THRESHOLD_PX } from '@/hooks/usePullToRefresh';
import { consumeHevyReturn } from '@/features/today/hevyDeepLink';

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

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

const DATE = '2026-11-09';

function renderToday({ hevyKey }: { hevyKey: boolean | null }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  const page = (
    <MemoryRouter initialEntries={['/today']}>
      <CurrentWorkoutPage />
    </MemoryRouter>
  );
  return render(
    <QueryClientProvider client={client}>
      {hevyKey === null ? page : <HevyStatusStub isConfigured={hevyKey}>{page}</HevyStatusStub>}
    </QueryClientProvider>
  );
}

function setOnline(online: boolean) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => online });
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'));
  });
}

function touch(type: 'touchstart' | 'touchmove' | 'touchend', clientY?: number) {
  const event = new Event(type, { bubbles: true });
  Object.defineProperty(event, 'touches', { value: clientY === undefined ? [] : [{ clientY }] });
  act(() => {
    document.body.dispatchEvent(event);
  });
}

describe('CurrentWorkoutPage with Hevy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    consumeHevyReturn();
    installPlansHandlers(plansHandlers.default());
    installWorkoutsHandlers(workoutsHandlers.cycle());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    installHevySyncHandlers(hevySyncHandlers.empty());
  });

  afterEach(() => {
    // Back to the real values, and tell TanStack Query's online manager (which listens for the event).
    delete (navigator as { onLine?: boolean }).onLine;
    delete (document as { visibilityState?: string }).visibilityState;
    window.dispatchEvent(new Event('online'));
  });

  it('without a stored key never pulls, asks for no inbox and offers no Hevy buttons', async () => {
    const pull = vi.spyOn(hevySyncApi, 'pull');
    const getInbox = vi.spyOn(hevySyncApi, 'getInbox');
    renderToday({ hevyKey: false });

    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    expect(screen.queryByTestId('hevy-inbox-chip')).not.toBeInTheDocument();
    expect(pull).not.toHaveBeenCalled();
    expect(getInbox).not.toHaveBeenCalled();
  });

  it('with no HevyProvider at all behaves as having no key', async () => {
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: null });

    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    expect(pull).not.toHaveBeenCalled();
  });

  it('pulls once on mount with a key, without holding up the page', async () => {
    let release: (() => void) | undefined;
    const pull = vi
      .spyOn(hevySyncApi, 'pull')
      .mockImplementation(() => new Promise((resolve) => (release = () => resolve(pullSummaryFixture()))));
    renderToday({ hevyKey: true });

    // The plan renders while the pull is still in flight.
    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    expect(pull).toHaveBeenCalledTimes(1);
    expect(pull).toHaveBeenCalledWith(false);

    await act(async () => release!());
    expect(pull).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('without a key Refresh just re-reads the plan', async () => {
    const getCycle = vi.spyOn(workoutsApi, 'getCurrentCycle');
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: false });
    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    expect(getCycle).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));

    await waitFor(() => expect(getCycle).toHaveBeenCalledTimes(2));
    expect(pull).not.toHaveBeenCalled();
  });

  it('Refresh pulls and names the session Hevy completed', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('today-refresh')).toBeEnabled());

    const pull = vi.spyOn(hevySyncApi, 'pull').mockImplementation(async () => {
      installPlansHandlers({ ...plansHandlers.default(), getToday: async (date) => todayDone(date ?? DATE) });
      return pullSummaryFixture({ pulled: 1, matched: 1 });
    });
    fireEvent.click(screen.getByRole('button', { name: 'Refresh and pull from Hevy' }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Matched Upper A from Hevy'));
    expect(pull).toHaveBeenCalledTimes(1);
    // The finished session shows as done; the page still shows the current session of the cycle.
    expect(await screen.findByTestId('last-session')).toHaveTextContent('Upper A done.');
    expect(screen.getByTestId('start-workout')).toBeInTheDocument();
  });

  it('Refresh with nothing new says so', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('today-refresh')).toBeEnabled());

    fireEvent.click(screen.getByTestId('today-refresh'));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Nothing new from Hevy'));
  });

  it('pulling down from the top of the page refreshes', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('today-refresh')).toBeEnabled());
    const pull = vi.spyOn(hevySyncApi, 'pull');

    touch('touchstart', 80);
    touch('touchmove', 80 + PULL_TO_REFRESH_THRESHOLD_PX + 20);
    expect(screen.getByTestId('pull-to-refresh-hint')).toHaveTextContent('Release to refresh');
    touch('touchend');

    await waitFor(() => expect(pull).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('pull-to-refresh-hint')).not.toBeInTheDocument();
  });

  it('a failed pull leaves the plan on screen with a retry that works', async () => {
    installHevySyncHandlers(hevySyncHandlers.writesFail());
    renderToday({ hevyKey: true });

    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    const notice = await screen.findByTestId('hevy-pull-failed');
    expect(notice).toHaveTextContent('Hevy is unavailable');

    installHevySyncHandlers(hevySyncHandlers.empty());
    fireEvent.click(screen.getByRole('button', { name: /retry/i }));

    await waitFor(() => expect(screen.queryByTestId('hevy-pull-failed')).not.toBeInTheDocument());
    expect(screen.getByTestId('start-workout')).toBeInTheDocument();
  });

  it('shows how many workouts wait in the inbox and links to it', async () => {
    installHevySyncHandlers(hevySyncHandlers.twoItems());
    renderToday({ hevyKey: true });

    const chip = await screen.findByTestId('hevy-inbox-chip');
    expect(chip).toHaveTextContent('2 unmatched Hevy workouts');
    expect(chip).toHaveAttribute('href', '/hevy/inbox');
  });

  it('offline with a key: no pull, a note, and Refresh explains instead of failing', async () => {
    setOnline(false);
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: true });

    expect(await screen.findByTestId('start-workout')).toBeInTheDocument();
    expect(screen.getByTestId('hevy-offline')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('today-refresh'));

    expect(pull).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(expect.stringMatching(/offline/i));

    // Back online: the pull that could not run on open runs now.
    setOnline(true);
    await waitFor(() => expect(pull).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('hevy-offline')).not.toBeInTheDocument();
  });
});
