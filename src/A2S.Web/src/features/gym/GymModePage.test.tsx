import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
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
import { mobilityA, planV3WithCardio, stairFinisher, upperAPlan } from './gymFixtures';
import { stairCardioItem } from '@/mocks/fixtures/cycle';
import { installReadinessHandlers, readinessHandlers } from '@/mocks/handlers/readiness';
import { rejectWith } from '@/mocks/handlers/install';

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

  describe('finisher and readiness', () => {
    beforeEach(async () => {
      const { plansApi } = await import('@/api/plans');
      const base = plansApi.getToday;
      plansApi.getToday = async (date) => {
        const today = await base(date);
        return today && {
          ...today,
          sessions: today.sessions.map((s) => (s.liftDay === 1 ? { ...s, finisherCardioProtocolId: stairFinisher.id } : s)),
        };
      };
      vi.spyOn(cardioProtocolsApi, 'list').mockResolvedValue([stairFinisher]);
    });

    it('shows the finisher on a normal day', async () => {
      renderPage();

      expect(await screen.findByText('Open Books')).toBeInTheDocument();
      expect(screen.getByText(/Finisher: Stair finisher/)).toBeInTheDocument();
    });

    it('hides the finisher on a red day while it is skipped for recovery', async () => {
      installReadinessHandlers(readinessHandlers.red());
      renderPage();

      expect(await screen.findByText('Open Books')).toBeInTheDocument();
      expect(screen.queryByText(/Finisher: Stair finisher/)).not.toBeInTheDocument();
    });

    it('keeps the finisher on a red day when the person tapped Undo', async () => {
      installReadinessHandlers(readinessHandlers.redKeptFinisher());
      renderPage();

      expect(await screen.findByText('Open Books')).toBeInTheDocument();
      expect(screen.getByText(/Finisher: Stair finisher/)).toBeInTheDocument();
    });
  });

  it('red readiness skips the stair climber but keeps the walk; Do it anyway brings it back', async () => {
    vi.spyOn(workoutsApi, 'getWeekPlan').mockResolvedValue(planV3WithCardio());
    installReadinessHandlers(readinessHandlers.red());
    renderPage();

    const stairs = await screen.findByTestId('cardio-v3-stairs');
    expect(stairs).toHaveTextContent('Skipped for recovery today');
    expect(screen.getByTestId('cardio-v3-walk')).not.toHaveTextContent('Skipped for recovery today');
    expect(screen.getByTestId('cardio-tick-v3-walk')).toBeInTheDocument();

    fireEvent.click(within(stairs).getByRole('button', { name: 'Do it anyway' }));

    expect(await screen.findByTestId('cardio-start-v3-stairs')).toBeInTheDocument();
  });

  it('Do it anyway works offline: un-skips on the phone, survives a reload and is sent on finish', async () => {
    const store = new Map<string, string>();
    vi.mocked(localStorage.getItem).mockImplementation((key: string) => store.get(key) ?? null);
    vi.mocked(localStorage.setItem).mockImplementation((key: string, value: string) => void store.set(key, value));
    vi.mocked(localStorage.removeItem).mockImplementation((key: string) => void store.delete(key));
    vi.mocked(localStorage.key).mockImplementation((i: number) => [...store.keys()][i] ?? null);
    Object.defineProperty(localStorage, 'length', { configurable: true, get: () => store.size });
    vi.spyOn(workoutsApi, 'getWeekPlan').mockResolvedValue({ ...planV3WithCardio(), workoutId: 'w-1', weekNumber: 3 });
    installReadinessHandlers({ ...readinessHandlers.red(), setFinisher: rejectWith(503) });
    const finish = vi.spyOn(gymSessionsApi, 'finish').mockResolvedValue(finished);
    const first = renderPage();

    fireEvent.click(within(await screen.findByTestId('cardio-v3-stairs')).getByRole('button', { name: 'Do it anyway' }));
    expect(await screen.findByTestId('cardio-start-v3-stairs')).toBeInTheDocument();
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    first.unmount();

    renderPage();
    const stairs = await screen.findByTestId('cardio-v3-stairs');
    expect(stairs).not.toHaveTextContent('Skipped for recovery today');
    fireEvent.click(within(stairs).getByRole('button', { name: /Solid/ }));
    fireEvent.click(screen.getByTestId('gym-finish-early'));
    fireEvent.click(within(screen.getByRole('dialog')).getByTestId('confirm-finish-early'));

    await waitFor(() => expect(finish).toHaveBeenCalledTimes(1));
    expect(finish.mock.calls[0][0].performances.map((p) => p.exerciseId)).toEqual(['v3-stairs']);
    store.clear();
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

  it('previews a cycle position by week: the planned sets for that week and the cardio item, Start only for the current session', async () => {
    const getWeekPlan = vi.spyOn(workoutsApi, 'getWeekPlan').mockResolvedValue({ ...upperAPlan, weekNumber: 4, cardio: stairCardioItem, isCurrent: false });
    renderPage(<GymModePage />, '/gym/1?preview=1&week=4');

    expect(await screen.findByTestId('preview-banner')).toBeInTheDocument();
    expect(getWeekPlan).toHaveBeenCalledWith('w-1', 4, 1);
    expect(screen.getByText(/Week 4 · Session 1 of 4/)).toBeInTheDocument();
    expect(screen.getByTestId('planned-cardio')).toHaveTextContent('5 min continuous @ level 6');
    expect(screen.getByTestId('planned-cardio')).toHaveTextContent('Cardio track: double progression · 2 more sessions to step up');
    expect(screen.queryByTestId('preview-start')).not.toBeInTheDocument();
    expect(localStorage.length).toBe(0);
  });

  it('shows the finisher outcome on the summary after Finish', async () => {
    vi.spyOn(gymSessionsApi, 'finish').mockResolvedValue({
      ...finished,
      hevyStatus: 'Posted',
      finisherOutcome: {
        kind: 'Track',
        name: 'Stair finisher',
        outcome: 'SteppedUp',
        reason: '2 qualifying sessions',
        qualified: true,
        progressed: true,
        before: '5 min continuous @ level 6',
        next: '6 min continuous @ level 6',
        sessionsToStepUp: 2,
        summary: 'Stair finisher: 5 min continuous @ level 6 → 6 min continuous @ level 6',
        nextTime: 'Next time: 6 min continuous @ level 6',
      },
    });
    renderPage();

    await tickOneSetAndFinishEarly();

    const outcome = await screen.findByTestId('finisher-outcome');
    expect(outcome).toHaveTextContent('Stair finisher: 5 min continuous @ level 6 → 6 min continuous @ level 6');
    expect(outcome).toHaveTextContent('Next time: 6 min continuous @ level 6');
  });

  it('preview of today offers Start into gym mode', async () => {
    renderPage(<GymModePage />, `/gym/1?preview=1`);

    expect(await screen.findByTestId('preview-start')).toHaveAttribute('href', '/gym/1');
    expect(localStorage.length).toBe(0);
  });
});
