import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Toaster } from 'react-hot-toast';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RoutinePlayerPage } from './RoutinePlayerPage';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { MOBILITY_YOGA_ID } from '@/mocks/fixtures/plan';

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-11') };
});

vi.mock('./playerEffects', () => ({ beep: vi.fn(), primeAudio: vi.fn(), useWakeLock: vi.fn() }));

function renderPlayer() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[`/routines/${MOBILITY_YOGA_ID}/play`]}>
        <Routes>
          <Route path="/routines/:id/play" element={<RoutinePlayerPage />} />
        </Routes>
      </MemoryRouter>
      <Toaster />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.useRealTimers();
  installPlansHandlers(plansHandlers.homeYoga());
});

describe('Routine progress on the Ready screen', () => {
  beforeEach(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.homePending()));

  it('lists each drill level and dose, the pending suggestion and recent changes with reasons', async () => {
    renderPlayer();

    expect(await screen.findByRole('heading', { name: 'Progress' })).toBeInTheDocument();
    expect(screen.getByText('Level 1 · 3 × 30 s')).toBeInTheDocument();
    expect(screen.getByText('Standard · 1 × 5 breaths')).toBeInTheDocument();
    expect(screen.getByText('Reverse plank ladder: move up to Level 2: Hands on a bench, straight legs?')).toBeInTheDocument();
    expect(screen.getByText(/2 clean sessions: 25 s to 30 s/)).toBeInTheDocument();
  });

  it('Accept moves the drill up a level and the prompt goes', async () => {
    const respond = vi.spyOn(mobilityRoutinesApi, 'respondToSuggestion');
    renderPlayer();

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }));

    await waitFor(() => expect(respond).toHaveBeenCalledWith(MOBILITY_YOGA_ID, 'Reverse plank ladder', true));
    expect(await screen.findByText('Level 2 · 3 × 15 s')).toBeInTheDocument();
    expect(screen.queryByTestId('level-suggestion')).not.toBeInTheDocument();
  });

  it('Not yet declines and keeps the level', async () => {
    const respond = vi.spyOn(mobilityRoutinesApi, 'respondToSuggestion');
    renderPlayer();

    fireEvent.click(await screen.findByRole('button', { name: 'Not yet' }));

    await waitFor(() => expect(respond).toHaveBeenCalledWith(MOBILITY_YOGA_ID, 'Reverse plank ladder', false));
    await waitFor(() => expect(screen.queryByTestId('level-suggestion')).not.toBeInTheDocument());
    expect(screen.getByText('Level 1 · 3 × 30 s')).toBeInTheDocument();
  });

  it('Undo last change calls the server', async () => {
    const undo = vi.spyOn(mobilityRoutinesApi, 'undoProgression');
    renderPlayer();

    fireEvent.click(await screen.findByTestId('undo-progression'));

    await waitFor(() => expect(undo).toHaveBeenCalledWith(MOBILITY_YOGA_ID));
  });

  it('says so when answering fails offline', async () => {
    installMobilityRoutinesHandlers({ ...mobilityRoutinesHandlers.homePending(), respondToSuggestion: () => Promise.reject(new Error('Network Error')) });
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    renderPlayer();

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }));

    expect(await screen.findByText("You're offline. Accept the new level when you're back online.")).toBeInTheDocument();
    online.mockRestore();
  });
});

describe('Level suggestion on the finish screen', () => {
  it('offers Accept / Not yet after the feedback suggests the next level', async () => {
    installMobilityRoutinesHandlers(mobilityRoutinesHandlers.homeLevelUp());
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const respond = vi.spyOn(mobilityRoutinesApi, 'respondToSuggestion');
    renderPlayer();

    fireEvent.click(await screen.findByTestId('start-routine'));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30000);
    });
    for (let i = 0; i < 4; i++) fireEvent.click(screen.getByRole('button', { name: 'Next step' }));
    fireEvent.click(screen.getByTestId('save-feedback'));

    expect(await screen.findByText('Next time: Level 2: Hands on a bench, straight legs')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Accept' }));

    await waitFor(() => expect(respond).toHaveBeenCalledWith(MOBILITY_YOGA_ID, 'Reverse plank ladder', true));
    await waitFor(() => expect(screen.queryByTestId('level-suggestion')).not.toBeInTheDocument());
    vi.useRealTimers();
  });
});
