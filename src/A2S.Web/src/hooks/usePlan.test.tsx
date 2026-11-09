import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { plansApi } from '@/api/plans';
import { planKeys, useToday, useCompleteSession, useActivePlan } from './usePlan';
import { todayUpperA, planFixture, PLAN_ID } from '@/mocks/fixtures/plan';
import { SessionKind } from '@/types/plan';

vi.mock('@/api/plans', () => ({
  plansApi: {
    getActive: vi.fn(),
    getToday: vi.fn(),
    getWeeklyGoals: vi.fn(),
    completeSession: vi.fn(),
    undoCompletion: vi.fn(),
    useAllowance: vi.fn(),
    removeAllowanceUse: vi.fn(),
  },
}));

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

describe('usePlan hooks', () => {
  beforeEach(() => {
    vi.mocked(plansApi.getToday).mockImplementation(async (date) => todayUpperA(date ?? '2026-11-09'));
    vi.mocked(plansApi.getActive).mockResolvedValue(planFixture);
  });

  it('useToday keys by date and passes the date to the API', async () => {
    const client = createClient();
    const { result } = renderHook(() => useToday('2026-11-09'), { wrapper: wrapperFor(client) });
    await waitFor(() => expect(result.current.data?.date).toBe('2026-11-09'));
    expect(plansApi.getToday).toHaveBeenCalledWith('2026-11-09');
    expect(client.getQueryData(planKeys.today('2026-11-09'))).toBeDefined();
    expect(planKeys.today('2026-11-09')).not.toEqual(planKeys.today('2026-11-10'));
  });

  it('useToday resolves to null when there is no plan', async () => {
    vi.mocked(plansApi.getToday).mockResolvedValue(null);
    const client = createClient();
    const { result } = renderHook(() => useToday('2026-11-09'), { wrapper: wrapperFor(client) });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });

  it('useCompleteSession posts the completion and invalidates today', async () => {
    const client = createClient();
    const wrapper = wrapperFor(client);
    const today = renderHook(() => useToday('2026-11-09'), { wrapper });
    await waitFor(() => expect(today.result.current.data).toBeDefined());
    vi.mocked(plansApi.completeSession).mockResolvedValue({
      id: 'c1',
      date: '2026-11-09',
      kind: SessionKind.Cardio,
      liftDay: null,
      cardioProtocolId: 'p',
      mobilityRoutineId: null,
      source: 0,
      hevyWorkoutId: null,
      recordedAtUtc: new Date().toISOString(),
      cardio: null,
      drillTicks: null,
    });

    const { result } = renderHook(() => useCompleteSession(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync({
        planId: PLAN_ID,
        request: { date: '2026-11-09', kind: SessionKind.Cardio, cardioProtocolId: 'p', cardio: { durationSeconds: 1800, avgHeartRate: null, rpe: null, distanceMeters: null, notes: null } },
      });
    });
    expect(plansApi.completeSession).toHaveBeenCalledWith(PLAN_ID, expect.objectContaining({ kind: SessionKind.Cardio }));
    await waitFor(() => expect(plansApi.getToday).toHaveBeenCalledTimes(2));
  });

  it('useActivePlan returns the plan', async () => {
    const client = createClient();
    const { result } = renderHook(() => useActivePlan(), { wrapper: wrapperFor(client) });
    await waitFor(() => expect(result.current.data?.id).toBe(PLAN_ID));
  });
});
