import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { HevyInboxPage } from './HevyInboxPage';
import { hevySyncApi } from '@/api/hevySync';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { INBOX_SUNDAY_LOWER_ID, INBOX_WEDNESDAY_UPPER_ID, pullSummaryFixture } from '@/mocks/fixtures/hevyInbox';
import { HevyStatusStub } from '@/mocks/HevyStatusStub';
import { SessionKind } from '@/types/plan';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'Joe' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  UserButton: () => <div data-testid="user-button" />,
  SignedIn: ({ children }: { children: ReactNode }) => <>{children}</>,
  SignedOut: () => null,
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

// Monday 9 November 2026: the fixtures put the workouts on Sunday 15 (rest) and Wednesday 11 (4×4 + Mobility B).
vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

function renderInbox({ hevyKey = true }: { hevyKey?: boolean } = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <HevyStatusStub isConfigured={hevyKey}>
        <MemoryRouter initialEntries={['/hevy/inbox']}>
          <HevyInboxPage />
        </MemoryRouter>
      </HevyStatusStub>
    </QueryClientProvider>
  );
}

function setOnline(online: boolean) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => online });
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'));
  });
}

describe('HevyInboxPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    installPlansHandlers(plansHandlers.week());
    installHevySyncHandlers(hevySyncHandlers.twoItems());
  });

  afterEach(() => {
    // Back to the real value, and tell TanStack Query's online manager (which listens for the event).
    delete (navigator as { onLine?: boolean }).onLine;
    window.dispatchEvent(new Event('online'));
  });

  it('lists each unmatched workout with its title, local date and exercises', async () => {
    renderInbox();

    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);
    expect(within(sunday).getByRole('heading', { name: 'Lower A' })).toBeInTheDocument();
    expect(within(sunday).getByText('Sun 15 Nov')).toBeInTheDocument();
    expect(within(sunday).getByText('4 exercises, 16 sets, 69 min')).toBeInTheDocument();
    expect(within(sunday).getByText('Squat (Barbell), Romanian Deadlift (Barbell), Leg Press + 1 more')).toBeInTheDocument();
    expect(within(sunday).getByText('Not matched: rest day')).toBeInTheDocument();

    const wednesday = screen.getByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`);
    expect(within(wednesday).getByText('Wed 11 Nov')).toBeInTheDocument();
    expect(within(wednesday).getByRole('button', { name: 'Assign Push day' })).toBeEnabled();
    expect(within(wednesday).getByRole('button', { name: 'Ignore Push day' })).toBeEnabled();
  });

  it('says so when nothing is waiting', async () => {
    installHevySyncHandlers(hevySyncHandlers.empty());
    renderInbox();

    expect(await screen.findByTestId('inbox-empty')).toHaveTextContent('Nothing waiting');
  });

  it("Assign lists that date's sessions and completes the one tapped", async () => {
    const assign = vi.spyOn(hevySyncApi, 'assign');
    renderInbox();
    const wednesday = await screen.findByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`);

    fireEvent.click(within(wednesday).getByTestId('inbox-assign'));

    const sheet = await screen.findByRole('dialog');
    expect(within(sheet).getByText('Sessions on Wed 11 Nov')).toBeInTheDocument();
    const sessions = await within(sheet).findAllByTestId('assign-to-session');
    expect(sessions.map((b) => b.textContent)).toEqual(['Norwegian 4×4Cardio', 'Mobility BMobility']);
    expect(within(sheet).getByTestId('assign-as-extra')).toBeInTheDocument();

    fireEvent.click(sessions[0]);

    await waitFor(() => expect(assign).toHaveBeenCalledTimes(1));
    expect(assign).toHaveBeenCalledWith(INBOX_WEDNESDAY_UPPER_ID, {
      date: '2026-11-11',
      kind: SessionKind.Cardio,
      liftDay: null,
      extra: false,
    });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(screen.queryByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`)).not.toBeInTheDocument());
    expect(screen.getByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`)).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Push day assigned to Norwegian 4×4');
  });

  it('a workout on a rest day has no session to pick, only Record as extra', async () => {
    const assign = vi.spyOn(hevySyncApi, 'assign');
    renderInbox();
    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);

    fireEvent.click(within(sunday).getByTestId('inbox-assign'));

    const sheet = await screen.findByRole('dialog');
    expect(await within(sheet).findByText('Nothing was planned that day.')).toBeInTheDocument();
    expect(within(sheet).queryByTestId('assign-to-session')).not.toBeInTheDocument();
    const extra = within(sheet).getByTestId('assign-as-extra');
    expect(extra).toHaveTextContent('Extra lift');
    await waitFor(() => expect(extra).toBeEnabled());

    fireEvent.click(extra);

    await waitFor(() => expect(assign).toHaveBeenCalledTimes(1));
    // "Lower A" is in the title, so the extra is recorded against Lower A's lift day.
    expect(assign).toHaveBeenCalledWith(INBOX_SUNDAY_LOWER_ID, { date: '2026-11-15', kind: SessionKind.Lift, liftDay: 2, extra: true });
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Lower A recorded as an extra session'));
  });

  it('Cancel closes the sheet without assigning', async () => {
    const assign = vi.spyOn(hevySyncApi, 'assign');
    renderInbox();
    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);
    fireEvent.click(within(sunday).getByTestId('inbox-assign'));
    const sheet = await screen.findByRole('dialog');

    fireEvent.click(within(sheet).getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(assign).not.toHaveBeenCalled();
  });

  it('Ignore dismisses the workout in one tap', async () => {
    const ignore = vi.spyOn(hevySyncApi, 'ignore');
    renderInbox();
    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);

    fireEvent.click(within(sunday).getByTestId('inbox-ignore'));

    await waitFor(() => expect(ignore).toHaveBeenCalledWith(INBOX_SUNDAY_LOWER_ID));
    await waitFor(() => expect(screen.queryByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`)).not.toBeInTheDocument());
    expect(screen.getByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`)).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Ignored Lower A');
  });

  it("a failed assign keeps the sheet open with the server's reason; a failed ignore keeps the item", async () => {
    installHevySyncHandlers(hevySyncHandlers.writesFail());
    renderInbox();
    const wednesday = await screen.findByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`);

    fireEvent.click(within(wednesday).getByTestId('inbox-assign'));
    const sheet = await screen.findByRole('dialog');
    fireEvent.click((await within(sheet).findAllByTestId('assign-to-session'))[0]);

    expect(await within(sheet).findByRole('alert')).toHaveTextContent('Hevy is unavailable');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    fireEvent.click(within(wednesday).getByTestId('inbox-ignore'));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Hevy is unavailable, your change is saved locally'));
    expect(screen.getByTestId(`inbox-item-${INBOX_WEDNESDAY_UPPER_ID}`)).toBeInTheDocument();
  });

  it('offline the inbox is read-only: the list shows, Assign, Ignore and Pull are off', async () => {
    renderInbox();
    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);

    setOnline(false);

    expect(screen.getByTestId('inbox-offline')).toHaveTextContent(/read-only/i);
    expect(within(sunday).getByTestId('inbox-assign')).toBeDisabled();
    expect(within(sunday).getByTestId('inbox-ignore')).toBeDisabled();
    expect(screen.getByTestId('inbox-pull')).toBeDisabled();

    setOnline(true);
    expect(screen.queryByTestId('inbox-offline')).not.toBeInTheDocument();
    expect(within(sunday).getByTestId('inbox-assign')).toBeEnabled();
  });

  it('going offline with the Assign sheet open closes it', async () => {
    renderInbox();
    const sunday = await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`);
    fireEvent.click(within(sunday).getByTestId('inbox-assign'));
    expect(await screen.findByRole('dialog')).toBeInTheDocument();

    setOnline(false);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('Pull from Hevy pulls and re-reads the inbox; a failed pull says the list is from before', async () => {
    installHevySyncHandlers(hevySyncHandlers.empty());
    renderInbox();
    expect(await screen.findByTestId('inbox-empty')).toBeInTheDocument();

    // The pull finds the two workouts.
    const found = hevySyncHandlers.twoItems();
    installHevySyncHandlers({ ...found, pull: async () => pullSummaryFixture({ pulled: 2, unmatched: 2 }) });
    fireEvent.click(screen.getByRole('button', { name: 'Pull from Hevy' }));

    expect(await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`)).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('2 new workouts in the Hevy inbox');

    installHevySyncHandlers(hevySyncHandlers.writesFail());
    fireEvent.click(screen.getByRole('button', { name: 'Pull from Hevy' }));

    expect(await screen.findByTestId('inbox-pull-failed')).toHaveTextContent('Hevy is unavailable');
    expect(screen.getByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`)).toBeInTheDocument();
  });

  it('shows an error with a retry when the inbox cannot be loaded', async () => {
    installHevySyncHandlers(hevySyncHandlers.error());
    renderInbox();

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't load the Hevy inbox");
    installHevySyncHandlers(hevySyncHandlers.twoItems());
    fireEvent.click(screen.getByRole('button', { name: /try again/i }));

    expect(await screen.findByTestId(`inbox-item-${INBOX_SUNDAY_LOWER_ID}`)).toBeInTheDocument();
  });

  it('without a Hevy key points to Settings and asks the server for nothing', async () => {
    const getInbox = vi.spyOn(hevySyncApi, 'getInbox');
    renderInbox({ hevyKey: false });

    expect(screen.getByTestId('inbox-not-connected')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Add your Hevy key in Settings' })).toHaveAttribute('href', '/settings');
    expect(screen.queryByTestId('inbox-pull')).not.toBeInTheDocument();
    expect(getInbox).not.toHaveBeenCalled();
  });
});
