import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { GymModePage } from './GymModePage';
import { workoutsApi } from '@/api/workouts';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { cardioProtocolsApi, gymSessionsApi } from '@/api/gymSessions';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { OfflineProvider } from '@/offline/OfflineProvider';
import { createQueue, type OfflineQueue } from '@/offline/queue';
import { createSyncManager, type SyncManager } from '@/offline/syncManager';
import type { WorkoutDto } from '@/features/workout/workoutSessionTypes';
import type { FinishGymSessionResult } from '@/types/gym';
import { mobilityA, upperAPlan } from './gymFixtures';

vi.mock('@/components/layout/Navbar', () => ({ Navbar: () => null }));
vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

const workout = { id: 'w-1', name: 'Cut to 85', currentWeek: 3, totalWeeks: 21, daysPerWeek: 4, exercises: [] } as unknown as WorkoutDto;

const finished: FinishGymSessionResult = {
  clientSessionId: 'x',
  completeDay: {
    workoutId: 'w-1',
    day: 1,
    weekNumber: 3,
    blockNumber: 1,
    exercisesCompleted: 1,
    progressionChanges: [{ exerciseId: 'ex-bench', exerciseName: 'Bench Press (Barbell)', change: 'TM +1%' }],
    newCurrentWeek: 3,
    newCurrentDay: 2,
    weekProgressed: false,
    programComplete: false,
    isDeloadWeek: false,
    exercisesPendingWeightConfirmation: [],
    nextSessionExercises: [],
  } as unknown as FinishGymSessionResult['completeDay'],
  completionId: 'c-1',
  finisherCompletionId: null,
  hevyStatus: 'Pending',
  hevyWorkoutId: null,
  replayed: false,
};

describe('GymModePage', () => {
  let online = true;
  let queue: OfflineQueue;
  let manager: SyncManager;

  function renderPage(children: ReactNode = <GymModePage />, path = '/gym/1') {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
    return render(
      <QueryClientProvider client={client}>
        <OfflineProvider manager={manager}>
          <MemoryRouter initialEntries={[path]}>
            <Routes>
              <Route path="/gym/:day" element={children} />
            </Routes>
          </MemoryRouter>
        </OfflineProvider>
      </QueryClientProvider>
    );
  }

  beforeEach(() => {
    online = true;
    queue = createQueue(`gym-${Math.random().toString(36).slice(2)}`);
    manager = createSyncManager({ queue, isOnline: () => online });
    installPlansHandlers(plansHandlers.default());
    vi.spyOn(workoutsApi, 'getCurrentWorkout').mockResolvedValue(workout);
    vi.spyOn(workoutsApi, 'getWeekPlan').mockResolvedValue(upperAPlan);
    vi.spyOn(mobilityRoutinesApi, 'list').mockResolvedValue([mobilityA]);
    vi.spyOn(cardioProtocolsApi, 'list').mockResolvedValue([]);
  });

  async function tickOneSetAndFinishEarly() {
    fireEvent.click(await screen.findByTestId('set-0-0'));
    fireEvent.click(screen.getByTestId('gym-finish-early'));
    fireEvent.click(within(screen.getByRole('dialog')).getByTestId('confirm-finish-early'));
  }

  it('loads Mobility A as the warm-up and finishes with the client session id, then shows Not in Hevy yet', async () => {
    const finish = vi.spyOn(gymSessionsApi, 'finish').mockResolvedValue(finished);
    renderPage();

    expect(await screen.findByText('Open Books')).toBeInTheDocument();
    await tickOneSetAndFinishEarly();

    await waitFor(() => expect(finish).toHaveBeenCalledTimes(1));
    const request = finish.mock.calls[0][0];
    expect(request.clientSessionId).toMatch(/^[0-9a-f-]{36}$/);
    expect(request.day).toBe(1);
    expect(request.performances).toHaveLength(1);
    expect(await screen.findByTestId('gym-hevy-status')).toHaveTextContent('Not in Hevy yet');
    expect(screen.queryByRole('button', { name: 'Send to Hevy' })).not.toBeInTheDocument();
  });

  it('queues the finish offline with the same client session id', async () => {
    online = false;
    const finish = vi.spyOn(gymSessionsApi, 'finish');
    renderPage();

    await tickOneSetAndFinishEarly();

    expect(await screen.findByText(/Saved on this phone/)).toBeInTheDocument();
    expect(finish).not.toHaveBeenCalled();
    const ops = await queue.all();
    expect(ops).toHaveLength(1);
    expect(ops[0].kind).toBe('gym.finish');
    expect(ops[0].target).toEqual({ clientSessionId: (ops[0].payload as { clientSessionId: string }).clientSessionId });
  });

  it('shows "already finished on another device" on a 409 and lets the draft be discarded', async () => {
    const conflict = Object.assign(new Error('Request failed with status code 409'), {
      isAxiosError: true,
      response: { status: 409, data: { detail: 'This day was already finished on another device' } },
    });
    vi.spyOn(gymSessionsApi, 'finish').mockRejectedValue(conflict);
    renderPage();

    await tickOneSetAndFinishEarly();

    const notice = await screen.findByTestId('gym-finished-elsewhere');
    expect(notice).toHaveTextContent('This day was already finished on another device');
    vi.mocked(localStorage.removeItem).mockClear();
    fireEvent.click(within(notice).getByRole('button', { name: "Discard this phone's session" }));
    expect(localStorage.removeItem).toHaveBeenCalledWith('a2s-gym-session:w-1:3:1');
  });

  it('preview is read-only: every set and the AMRAP target shown, nothing tickable, no draft saved, no Start on another day', async () => {
    localStorage.clear();
    const finish = vi.spyOn(gymSessionsApi, 'finish');
    renderPage(<GymModePage />, '/gym/1?preview=1&date=2030-01-08');

    expect(await screen.findByTestId('preview-banner')).toHaveTextContent('Preview: weights can change if you finish sessions before then');
    expect(screen.getByText('Open Books')).toBeInTheDocument();
    expect(within(screen.getByTestId('preview-lift-0')).getAllByRole('listitem')).toHaveLength(5);
    expect(screen.getByTestId('preview-lift-0')).toHaveTextContent('75 kg × 5+');
    expect(screen.getByTestId('preview-lift-0')).toHaveTextContent('AMRAP target: 5+ reps');
    expect(screen.queryByTestId('set-0-0')).not.toBeInTheDocument();
    expect(screen.queryByTestId('gym-finish-early')).not.toBeInTheDocument();
    expect(screen.queryByTestId('preview-start')).not.toBeInTheDocument();
    expect(localStorage.length).toBe(0);
    expect(await queue.all()).toHaveLength(0);
    expect(finish).not.toHaveBeenCalled();
  });

  it('preview of today offers Start into gym mode', async () => {
    renderPage(<GymModePage />, `/gym/1?preview=1`);

    expect(await screen.findByTestId('preview-start')).toHaveAttribute('href', '/gym/1');
    expect(localStorage.length).toBe(0);
  });
});
