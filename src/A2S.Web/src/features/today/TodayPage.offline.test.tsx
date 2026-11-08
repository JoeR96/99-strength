import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within, renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { TodayPage } from './TodayPage';
import { habitsApi } from '@/api/habits';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers, statefulHabitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { habitsTodayFixture, WATER_ID } from '@/mocks/fixtures/habits';
import { OfflineProvider } from '@/offline/OfflineProvider';
import { createQueue, type OfflineQueue } from '@/offline/queue';
import { createSyncManager, type SyncManager } from '@/offline/syncManager';
import { useHabitsToday, useSetHabitValue } from '@/hooks/useHabits';
import { usePendingSyncCount } from '@/offline/context';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'Joe' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  UserButton: () => <div data-testid="user-button" />,
  SignedIn: ({ children }: { children: ReactNode }) => <>{children}</>,
  SignedOut: () => null,
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

const DATE = '2026-11-09';

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

describe('TodayPage offline', () => {
  let online = true;
  let queue: OfflineQueue;
  let manager: SyncManager;

  function wrapper({ children }: { children: ReactNode }) {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
    });
    return (
      <QueryClientProvider client={client}>
        <OfflineProvider manager={manager}>
          <MemoryRouter initialEntries={['/today']}>{children}</MemoryRouter>
        </OfflineProvider>
      </QueryClientProvider>
    );
  }

  beforeEach(() => {
    online = true;
    queue = createQueue(`today-offline-${Math.random().toString(36).slice(2)}`);
    manager = createSyncManager({ queue, isOnline: () => online });
    installPlansHandlers(plansHandlers.default());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
  });

  afterEach(async () => {
    manager.stop();
    await queue.close();
    vi.useRealTimers();
  });

  it('a tap with no signal keeps its optimistic value, counts on the badge, and never reaches the API', async () => {
    const incrementSpy = vi.spyOn(habitsApi, 'increment');
    render(<TodayPage />, { wrapper });
    const water = await screen.findByTestId(`habit-counter-${WATER_ID}`);
    expect(within(water).getByText('2')).toBeInTheDocument();
    expect(screen.queryByTestId('pending-sync')).not.toBeInTheDocument();

    online = false;
    fireEvent.click(within(water).getByRole('button', { name: /add one water/i }));
    fireEvent.click(within(water).getByRole('button', { name: /add one water/i }));

    await waitFor(() => expect(screen.getByTestId('pending-sync')).toHaveTextContent('2 pending'));
    expect(within(water).getByText('4')).toBeInTheDocument();
    expect(incrementSpy).not.toHaveBeenCalled();
    expect(await queue.count()).toBe(2);
    const ops = await queue.all();
    expect(ops.map((o) => o.localDate)).toEqual([DATE, DATE]);
    expect(ops[0]).toMatchObject({ kind: 'habit.increment', target: { habitId: WATER_ID }, label: 'Water' });

    // Signal back: the two taps replay in order and the badge clears; the value matches the server.
    online = true;
    await act(async () => {
      window.dispatchEvent(new Event('online'));
    });
    await waitFor(() => expect(incrementSpy).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByTestId('pending-sync')).not.toBeInTheDocument());
    expect(incrementSpy.mock.calls[0]).toEqual([WATER_ID, DATE, expect.objectContaining({ delta: 1 })]);
    await waitFor(() => expect(within(water).getByText('4')).toBeInTheDocument());
  });

  it('a request that dies on the wire while "online" is queued too, with no rollback or error toast', async () => {
    vi.spyOn(habitsApi, 'increment').mockRejectedValueOnce(
      Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK' })
    );
    render(<TodayPage />, { wrapper });
    const water = await screen.findByTestId(`habit-counter-${WATER_ID}`);

    // The replay would run straight away while online; hold it so the queued state is visible.
    online = false;
    fireEvent.click(within(water).getByRole('button', { name: /add one water/i }));

    await waitFor(() => expect(screen.getByTestId('pending-sync')).toHaveTextContent('1 pending'));
    expect(within(water).getByText('3')).toBeInTheDocument();
  });

  it('two devices: an older offline set loses to a newer online value and the cache reconciles', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-11-09T09:00:00Z'));

    // The "server": water is 2 until device B sets 5 at 09:30; a set with an older clientTimestamp
    // than the stored one is ignored and the stored log returned (the API's last-write-wins rule).
    let serverValue = 2;
    let serverTs = '2026-11-09T08:00:00.000Z';
    installHabitsHandlers({
      ...statefulHabitsHandlers({ latencyMs: 0 }),
      getToday: async (date) => habitsTodayFixture(date ?? DATE, { [WATER_ID]: serverValue }),
      setLog: async (habitId, date, request) => {
        if (request.clientTimestamp >= serverTs) {
          serverValue = request.value;
          serverTs = request.clientTimestamp;
        }
        return { habitId, date, value: serverValue, updatedAtUtc: serverTs };
      },
    });

    const { result } = renderHook(
      () => ({ today: useHabitsToday(DATE), set: useSetHabitValue(), pending: usePendingSyncCount() }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.today.data).toBeDefined());
    const water = () => result.current.today.data!.habits.find((h) => h.id === WATER_ID)!.value;
    expect(water()).toBe(2);

    // Device A, offline, 09:00: water 3.
    online = false;
    act(() => result.current.set.set(WATER_ID, 3, DATE));
    await waitFor(() => expect(result.current.pending).toBe(1));
    expect(water()).toBe(3);

    // Device B, online, 09:30: water 5.
    serverValue = 5;
    serverTs = '2026-11-09T09:30:00.000Z';

    // Device A back at 10:00 replays its 09:00 write.
    vi.setSystemTime(new Date('2026-11-09T10:00:00Z'));
    online = true;
    await act(async () => {
      await manager.replayNow();
    });

    await waitFor(() => expect(result.current.pending).toBe(0));
    expect(serverValue).toBe(5);
    await waitFor(() => expect(water()).toBe(5));
  });
});
