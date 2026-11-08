import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { habitsApi } from '@/api/habits';
import { habitKeys, useHabitsToday, useIncrementHabit, useSetHabitValue } from './useHabits';
import { habitsTodayFixture, WATER_ID } from '@/mocks/fixtures/habits';
import type { HabitLogDto, HabitsTodayDto } from '@/types/habits';
import { todayLocal } from '@/lib/localDate';
import toast from 'react-hot-toast';

vi.mock('@/api/habits', () => ({
  habitsApi: {
    getToday: vi.fn(),
    increment: vi.fn(),
    setLog: vi.fn(),
    getHistory: vi.fn(),
    list: vi.fn(),
  },
}));

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

vi.mock('react-hot-toast', () => ({
  default: { error: vi.fn(), success: vi.fn() },
}));

const DATE = '2026-11-09';
/** The fake server's stored water value; `getToday` reads it so a refetch after settle agrees with the writes. */
let serverWater = 2;

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function waterValue(client: QueryClient, date = DATE): number | undefined {
  return client.getQueryData<HabitsTodayDto>(habitKeys.today(date))?.habits.find((h) => h.id === WATER_ID)?.value;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function log(value: number): HabitLogDto {
  serverWater = value;
  return { habitId: WATER_ID, date: DATE, value, updatedAtUtc: new Date().toISOString() };
}

describe('useHabitsToday', () => {
  beforeEach(() => {
    vi.mocked(todayLocal).mockReturnValue(DATE);
    serverWater = 2;
    vi.mocked(habitsApi.getToday).mockImplementation(async (date) =>
      habitsTodayFixture(date ?? DATE, { [WATER_ID]: serverWater })
    );
  });

  it('keys the query by date so the page rolls over without a reload', async () => {
    const client = createClient();
    const { result, rerender } = renderHook(({ date }) => useHabitsToday(date), {
      wrapper: wrapperFor(client),
      initialProps: { date: '2026-11-09' },
    });
    await waitFor(() => expect(result.current.data?.date).toBe('2026-11-09'));

    rerender({ date: '2026-11-10' });
    await waitFor(() => expect(result.current.data?.date).toBe('2026-11-10'));

    expect(habitsApi.getToday).toHaveBeenCalledWith('2026-11-09');
    expect(habitsApi.getToday).toHaveBeenCalledWith('2026-11-10');
    expect(client.getQueryData(habitKeys.today('2026-11-10'))).toBeDefined();
    expect(habitKeys.today('2026-11-09')).not.toEqual(habitKeys.today('2026-11-10'));
  });
});

describe('useIncrementHabit', () => {
  beforeEach(() => {
    vi.mocked(todayLocal).mockReturnValue(DATE);
    serverWater = 2;
    vi.mocked(habitsApi.getToday).mockImplementation(async (date) =>
      habitsTodayFixture(date ?? DATE, { [WATER_ID]: serverWater })
    );
  });

  async function setup() {
    const client = createClient();
    const wrapper = wrapperFor(client);
    const today = renderHook(() => useHabitsToday(DATE), { wrapper });
    await waitFor(() => expect(today.result.current.data).toBeDefined());
    const increment = renderHook(() => useIncrementHabit(), { wrapper });
    return { client, today, increment };
  }

  it('updates the cache immediately and keeps the server value when it agrees', async () => {
    const { client, increment } = await setup();
    const request = deferred<HabitLogDto>();
    vi.mocked(habitsApi.increment).mockReturnValue(request.promise);

    act(() => increment.result.current.tap(WATER_ID, 1));
    await waitFor(() => expect(waterValue(client)).toBe(3));

    await act(async () => {
      request.resolve(log(3));
      await request.promise;
    });
    await waitFor(() => expect(increment.result.current.isPending).toBe(false));
    expect(waterValue(client)).toBe(3);
    expect(habitsApi.increment).toHaveBeenCalledWith(
      WATER_ID,
      DATE,
      expect.objectContaining({ delta: 1, clientTimestamp: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T/) })
    );
  });

  it('double-tap sends two increments and settles on 4, never 3 or 5', async () => {
    const { client, increment } = await setup();
    const first = deferred<HabitLogDto>();
    const second = deferred<HabitLogDto>();
    vi.mocked(habitsApi.increment).mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const seen: number[] = [];
    const unsubscribe = client.getQueryCache().subscribe(() => {
      const v = waterValue(client);
      if (v !== undefined) seen.push(v);
    });

    act(() => {
      increment.result.current.tap(WATER_ID, 1);
      increment.result.current.tap(WATER_ID, 1);
    });
    await waitFor(() => expect(waterValue(client)).toBe(4));

    // Server replies in order: 3 then 4. The first reply must not drag the cache back to 3.
    await act(async () => {
      first.resolve(log(3));
      await first.promise;
    });
    expect(waterValue(client)).toBe(4);
    await act(async () => {
      second.resolve(log(4));
      await second.promise;
    });
    await waitFor(() => expect(client.isMutating()).toBe(0));
    unsubscribe();

    expect(habitsApi.increment).toHaveBeenCalledTimes(2);
    expect(waterValue(client)).toBe(4);
    expect(seen).not.toContain(5);
    expect(seen.slice(seen.indexOf(4))).not.toContain(3);
  });

  it('rolls back and toasts when the server fails', async () => {
    const { client, increment } = await setup();
    const request = deferred<HabitLogDto>();
    vi.mocked(habitsApi.increment).mockReturnValue(request.promise);

    act(() => increment.result.current.tap(WATER_ID, 1));
    await waitFor(() => expect(waterValue(client)).toBe(3));

    await act(async () => {
      request.reject(Object.assign(new Error('boom'), { response: { status: 500 } }));
      await request.promise.catch(() => {});
    });
    await waitFor(() => expect(increment.result.current.isError).toBe(true));

    expect(waterValue(client)).toBe(2);
    expect(toast.error).toHaveBeenCalled();
  });

  it('never goes below zero optimistically', async () => {
    const { client, increment } = await setup();
    vi.mocked(habitsApi.increment).mockResolvedValue(log(0));
    act(() => {
      increment.result.current.tap(WATER_ID, -1);
      increment.result.current.tap(WATER_ID, -1);
      increment.result.current.tap(WATER_ID, -1);
    });
    await waitFor(() => expect(waterValue(client)).toBe(0));
    await waitFor(() => expect(client.isMutating()).toBe(0));
    expect(waterValue(client)).toBe(0);
  });

  it('writes to the local date at tap time: 23:59 and 00:01 go to different days', async () => {
    const { increment } = await setup();
    vi.mocked(habitsApi.increment).mockImplementation(async (_id, date) => ({ ...log(1), date }));

    vi.mocked(todayLocal).mockReturnValue('2026-11-09');
    act(() => increment.result.current.tap(WATER_ID, 1));
    await waitFor(() => expect(habitsApi.increment).toHaveBeenCalledTimes(1));

    vi.mocked(todayLocal).mockReturnValue('2026-11-10');
    act(() => increment.result.current.tap(WATER_ID, 1));
    await waitFor(() => expect(habitsApi.increment).toHaveBeenCalledTimes(2));

    expect(vi.mocked(habitsApi.increment).mock.calls[0][1]).toBe('2026-11-09');
    expect(vi.mocked(habitsApi.increment).mock.calls[1][1]).toBe('2026-11-10');
  });
});

describe('useSetHabitValue', () => {
  beforeEach(() => {
    vi.mocked(todayLocal).mockReturnValue(DATE);
    serverWater = 2;
    vi.mocked(habitsApi.getToday).mockImplementation(async (date) =>
      habitsTodayFixture(date ?? DATE, { [WATER_ID]: serverWater })
    );
  });

  it('sets the value optimistically with a clientTimestamp and reconciles with the server', async () => {
    const client = createClient();
    const wrapper = wrapperFor(client);
    const today = renderHook(() => useHabitsToday(DATE), { wrapper });
    await waitFor(() => expect(today.result.current.data).toBeDefined());
    vi.mocked(habitsApi.setLog).mockResolvedValue(log(5));

    const { result } = renderHook(() => useSetHabitValue(), { wrapper });
    act(() => result.current.set(WATER_ID, 5));
    await waitFor(() => expect(waterValue(client)).toBe(5));
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(habitsApi.setLog).toHaveBeenCalledWith(
      WATER_ID,
      DATE,
      expect.objectContaining({ value: 5, clientTimestamp: expect.any(String) })
    );
  });
});
