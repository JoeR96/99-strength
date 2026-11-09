import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { TodayPage } from './TodayPage';
import { hevySyncApi } from '@/api/hevySync';
import { plansApi } from '@/api/plans';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { hevySyncHandlers, installHevySyncHandlers, installPushFlowHandlers } from '@/mocks/handlers/hevySync';
import { pullSummaryFixture } from '@/mocks/fixtures/hevyInbox';
import { HEVY_ROUTINE_ID, PLAN_ID, todayDone } from '@/mocks/fixtures/plan';
import { HevyStatusStub } from '@/mocks/HevyStatusStub';
import { PULL_TO_REFRESH_THRESHOLD_PX } from '@/hooks/usePullToRefresh';
import { consumeHevyReturn, openHevyRoutine } from './hevyDeepLink';

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

// The real hand-over, minus actually navigating jsdom to hevy:// or opening hevy.com.
vi.mock('./hevyDeepLink', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./hevyDeepLink')>();
  return {
    ...actual,
    openHevyRoutine: vi.fn((routineId: string) =>
      actual.openHevyRoutine(routineId, { navigate: () => {}, openWeb: () => {}, isVisible: () => false })
    ),
  };
});

const DATE = '2026-11-09';

function renderToday({ hevyKey }: { hevyKey: boolean | null }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  const page = (
    <MemoryRouter initialEntries={['/today']}>
      <TodayPage />
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

function setVisibility(state: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state });
  act(() => {
    document.dispatchEvent(new Event('visibilitychange'));
  });
}

function touch(type: 'touchstart' | 'touchmove' | 'touchend', clientY?: number) {
  const event = new Event(type, { bubbles: true });
  Object.defineProperty(event, 'touches', { value: clientY === undefined ? [] : [{ clientY }] });
  act(() => {
    document.body.dispatchEvent(event);
  });
}

describe('TodayPage with Hevy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    consumeHevyReturn();
    installPlansHandlers(plansHandlers.default());
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

    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    expect(screen.queryByTestId('hevy-inbox-chip')).not.toBeInTheDocument();
    expect(pull).not.toHaveBeenCalled();
    expect(getInbox).not.toHaveBeenCalled();
  });

  it('with no HevyProvider at all behaves as having no key', async () => {
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: null });

    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(pull).not.toHaveBeenCalled();
  });

  it('pulls once on mount with a key, without holding up the page', async () => {
    let release: (() => void) | undefined;
    const pull = vi
      .spyOn(hevySyncApi, 'pull')
      .mockImplementation(() => new Promise((resolve) => (release = () => resolve(pullSummaryFixture()))));
    renderToday({ hevyKey: true });

    // The plan renders while the pull is still in flight.
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(pull).toHaveBeenCalledTimes(1);
    expect(pull).toHaveBeenCalledWith(false);

    await act(async () => release!());
    expect(pull).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('without a key Refresh just re-reads the plan', async () => {
    const getToday = vi.spyOn(plansApi, 'getToday');
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: false });
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(getToday).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));

    await waitFor(() => expect(getToday).toHaveBeenCalledTimes(2));
    expect(pull).not.toHaveBeenCalled();
  });

  it('Refresh pulls and names the session Hevy completed', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('today-refresh')).toBeEnabled());

    const pull = vi.spyOn(hevySyncApi, 'pull').mockImplementation(async () => {
      installPlansHandlers({ ...plansHandlers.default(), getToday: async (date) => todayDone(date ?? DATE) });
      return pullSummaryFixture({ pulled: 1, matched: 1 });
    });
    fireEvent.click(screen.getByRole('button', { name: 'Refresh and pull from Hevy' }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Matched Upper A from Hevy'));
    expect(pull).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByTestId('start-session')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
  });

  it('Refresh with nothing new says so', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('today-refresh')).toBeEnabled());

    fireEvent.click(screen.getByTestId('today-refresh'));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Nothing new from Hevy'));
  });

  it('pulling down from the top of the page refreshes', async () => {
    renderToday({ hevyKey: true });
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
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

    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    const notice = await screen.findByTestId('hevy-pull-failed');
    expect(notice).toHaveTextContent('Hevy is unavailable');

    installHevySyncHandlers(hevySyncHandlers.empty());
    fireEvent.click(screen.getByRole('button', { name: /retry/i }));

    await waitFor(() => expect(screen.queryByTestId('hevy-pull-failed')).not.toBeInTheDocument());
    expect(screen.getByTestId('start-session')).toBeInTheDocument();
  });

  it('shows how many workouts wait in the inbox and links to it', async () => {
    installHevySyncHandlers(hevySyncHandlers.twoItems());
    renderToday({ hevyKey: true });

    const chip = await screen.findByTestId('hevy-inbox-chip');
    expect(chip).toHaveTextContent('2 unmatched Hevy workouts');
    expect(chip).toHaveAttribute('href', '/hevy/inbox');
  });

  it('Push to Hevy posts the session, then the card offers Open in Hevy', async () => {
    installPushFlowHandlers();
    const pushSession = vi.spyOn(hevySyncApi, 'pushSession');
    renderToday({ hevyKey: true });

    fireEvent.click(await screen.findByTestId('push-to-hevy'));

    await waitFor(() => expect(pushSession).toHaveBeenCalledTimes(1));
    expect(pushSession).toHaveBeenCalledWith(PLAN_ID, { date: DATE, kind: 1, liftDay: 1 });
    expect(await screen.findByTestId('open-in-hevy')).toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Upper A is in Hevy');
  });

  it("a push Hevy refuses shows the server's reason and keeps the button", async () => {
    installHevySyncHandlers(hevySyncHandlers.writesFail());
    renderToday({ hevyKey: true });

    fireEvent.click(await screen.findByTestId('push-to-hevy'));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Hevy is unavailable, your change is saved locally'));
    expect(screen.getByTestId('push-to-hevy')).toBeEnabled();
  });

  it('Open in Hevy opens the routine, and coming back pulls again', async () => {
    installPlansHandlers(plansHandlers.withHevyRoutine());
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderToday({ hevyKey: true });

    const open = await screen.findByTestId('open-in-hevy');
    await waitFor(() => expect(pull).toHaveBeenCalledTimes(1));
    fireEvent.click(open);
    expect(openHevyRoutine).toHaveBeenCalledWith(HEVY_ROUTINE_ID);

    setVisibility('hidden');
    expect(pull).toHaveBeenCalledTimes(1);
    setVisibility('visible');

    await waitFor(() => expect(pull).toHaveBeenCalledTimes(2));
  });

  it('offline with a key: no pull, a note, and Push to Hevy explains instead of failing', async () => {
    setOnline(false);
    const pull = vi.spyOn(hevySyncApi, 'pull');
    const pushSession = vi.spyOn(hevySyncApi, 'pushSession');
    renderToday({ hevyKey: true });

    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(screen.getByTestId('hevy-offline')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('push-to-hevy'));
    fireEvent.click(screen.getByTestId('today-refresh'));

    expect(pull).not.toHaveBeenCalled();
    expect(pushSession).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/offline/i));

    // Back online: the pull that could not run on open runs now.
    setOnline(true);
    await waitFor(() => expect(pull).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('hevy-offline')).not.toBeInTheDocument();
  });
});
