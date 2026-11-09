import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';
import { useSyncWorkoutToHevy, useSyncDayToHevy } from './useHevySync';
import type { WorkoutDto } from '@/types/workout';

vi.mock('@/services/hevySyncService', () => ({
  syncWorkoutToHevy: vi.fn(),
  syncDayToHevy: vi.fn(),
}));

vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

import { syncWorkoutToHevy, syncDayToHevy } from '@/services/hevySyncService';
import toast from 'react-hot-toast';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const mockWorkout: WorkoutDto = {
  id: 'w-1',
  name: 'Test',
  variant: 4,
  status: 2,
  currentWeek: 1,
  currentBlock: 1,
  currentDay: 1,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [],
  isWeekComplete: false,
  totalWeeks: 21,
  startDate: '2024-01-01',
  createdAt: '2024-01-01',
  exerciseCount: 0,
  exercises: [],
};

describe('useSyncWorkoutToHevy', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls syncWorkoutToHevy on mutate', async () => {
    vi.mocked(syncWorkoutToHevy).mockResolvedValue({
      success: true,
      message: 'Synced!',
    });

    const { result } = renderHook(() => useSyncWorkoutToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(mockWorkout);
    });

    await vi.waitFor(() => {
      expect(syncWorkoutToHevy).toHaveBeenCalledWith(mockWorkout);
    });
  });

  it('shows success toast on successful sync', async () => {
    vi.mocked(syncWorkoutToHevy).mockResolvedValue({
      success: true,
      message: 'Synced!',
    });

    const { result } = renderHook(() => useSyncWorkoutToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(mockWorkout);
    });

    await vi.waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith('Synced!');
    });
  });

  it('shows error toast on failed sync', async () => {
    vi.mocked(syncWorkoutToHevy).mockResolvedValue({
      success: false,
      message: 'Failed!',
    });

    const { result } = renderHook(() => useSyncWorkoutToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(mockWorkout);
    });

    await vi.waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Failed!');
    });
  });

  it('shows error toast on exception', async () => {
    vi.mocked(syncWorkoutToHevy).mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useSyncWorkoutToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(mockWorkout);
    });

    await vi.waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith('Sync failed: Network error');
    });
  });
});

describe('useSyncDayToHevy', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls syncDayToHevy with workout and day', async () => {
    vi.mocked(syncDayToHevy).mockResolvedValue({
      success: true,
      message: 'Day synced!',
    });

    const { result } = renderHook(() => useSyncDayToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate({ workout: mockWorkout, dayNumber: 2 });
    });

    await vi.waitFor(() => {
      expect(syncDayToHevy).toHaveBeenCalledWith(mockWorkout, 2);
    });
  });

  it('shows success toast on successful day sync', async () => {
    vi.mocked(syncDayToHevy).mockResolvedValue({
      success: true,
      message: 'Day synced!',
    });

    const { result } = renderHook(() => useSyncDayToHevy(), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate({ workout: mockWorkout, dayNumber: 1 });
    });

    await vi.waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith('Day synced!');
    });
  });
});
