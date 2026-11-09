import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import toast from 'react-hot-toast';
import { HevySyncSettings } from './HevySyncSettings';
import { hevySyncApi } from '@/api/hevySync';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { pullSummaryFixture } from '@/mocks/fixtures/hevyInbox';
import { PLAN_ID } from '@/mocks/fixtures/plan';
import { HevyStatusStub } from '@/mocks/HevyStatusStub';

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }),
}));

function renderSettings({ hevyKey = true }: { hevyKey?: boolean } = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <HevyStatusStub isConfigured={hevyKey}>
        <MemoryRouter initialEntries={['/settings']}>
          <HevySyncSettings />
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

describe('HevySyncSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    installPlansHandlers(plansHandlers.default());
    installHevySyncHandlers(hevySyncHandlers.empty());
  });

  afterEach(() => {
    // Back to the real value, and tell TanStack Query's online manager (which listens for the event).
    delete (navigator as { onLine?: boolean }).onLine;
    window.dispatchEvent(new Event('online'));
  });

  it('is not shown without a Hevy key', () => {
    const { container } = renderSettings({ hevyKey: false });
    expect(container).toBeEmptyDOMElement();
  });

  it('Push this week sends the active plan and lists what happened day by day', async () => {
    const pushWeek = vi.spyOn(hevySyncApi, 'pushWeek');
    renderSettings();
    const button = screen.getByTestId('hevy-push-week');
    await waitFor(() => expect(button).toBeEnabled());

    fireEvent.click(button);

    const result = await screen.findByTestId('hevy-push-week-result');
    expect(pushWeek).toHaveBeenCalledWith(PLAN_ID, undefined);
    expect(within(result).getByText('Week of Mon 9 Nov')).toBeInTheDocument();
    const rows = within(result).getAllByRole('listitem').map((li) => li.textContent);
    expect(rows).toEqual([
      'Mon 9 NovUpper ACreated',
      'Tue 10 NovLower ACreated',
      'Wed 11 NovNorwegian 4×4Updated',
      'Wed 11 NovMobility BUpdated',
      'Thu 12 NovUpper BCreated',
      'Fri 13 NovLower BCreated',
      'Sat 14 NovC25K runNot pushed: This cardio slot has no protocol; choose one before pushing it to Hevy.',
    ]);
  });

  it('a week push that fails outright says why', async () => {
    installHevySyncHandlers(hevySyncHandlers.writesFail());
    renderSettings();
    const button = screen.getByTestId('hevy-push-week');
    await waitFor(() => expect(button).toBeEnabled());

    fireEvent.click(button);

    expect(await screen.findByRole('alert')).toHaveTextContent('Hevy is unavailable, your change is saved locally');
    expect(screen.queryByTestId('hevy-push-week-result')).not.toBeInTheDocument();
  });

  it('without a plan there is nothing to push, but Pull now still works', async () => {
    installPlansHandlers(plansHandlers.noPlan());
    renderSettings();

    expect(await screen.findByText(/start a plan on today first/i)).toBeInTheDocument();
    expect(screen.getByTestId('hevy-push-week')).toBeDisabled();
    expect(screen.getByTestId('hevy-pull-now')).toBeEnabled();
  });

  it('Pull now shows the summary in place rather than as a toast', async () => {
    installHevySyncHandlers({
      ...hevySyncHandlers.empty(),
      pull: async () => pullSummaryFixture({ pulled: 3, matched: 2, unmatched: 1 }),
    });
    const pull = vi.spyOn(hevySyncApi, 'pull');
    renderSettings();

    fireEvent.click(screen.getByTestId('hevy-pull-now'));

    const result = await screen.findByTestId('hevy-pull-result');
    expect(pull).toHaveBeenCalledWith(false);
    expect(result).toHaveTextContent('Matched 2 workouts from Hevy, 1 in the inbox.');
    expect(result).toHaveTextContent('3 pulled, 2 matched, 1 unmatched');
    expect(within(result).getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/hevy/inbox');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('Pull now with nothing new, and with no plan to pull into', async () => {
    renderSettings();
    fireEvent.click(screen.getByTestId('hevy-pull-now'));
    expect(await screen.findByTestId('hevy-pull-result')).toHaveTextContent('Nothing new from Hevy.');
    expect(screen.queryByRole('link', { name: 'Open the inbox' })).not.toBeInTheDocument();

    installHevySyncHandlers({
      ...hevySyncHandlers.empty(),
      pull: async () => pullSummaryFixture({ wasSkipped: true, skippedReason: 'no active plan' }),
    });
    fireEvent.click(screen.getByTestId('hevy-pull-now'));
    await waitFor(() => expect(screen.getByTestId('hevy-pull-result')).toHaveTextContent('Nothing pulled: no active plan.'));
    expect(screen.getByTestId('hevy-pull-result')).not.toHaveTextContent('0 pulled');
  });

  it('a failed pull says why', async () => {
    installHevySyncHandlers(hevySyncHandlers.writesFail());
    renderSettings();

    fireEvent.click(screen.getByTestId('hevy-pull-now'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Hevy is unavailable');
    expect(screen.queryByTestId('hevy-pull-result')).not.toBeInTheDocument();
  });

  it('offline both actions are off with a note', async () => {
    renderSettings();
    await waitFor(() => expect(screen.getByTestId('hevy-push-week')).toBeEnabled());

    setOnline(false);

    expect(screen.getByTestId('hevy-sync-offline')).toBeInTheDocument();
    expect(screen.getByTestId('hevy-push-week')).toBeDisabled();
    expect(screen.getByTestId('hevy-pull-now')).toBeDisabled();
  });
});
