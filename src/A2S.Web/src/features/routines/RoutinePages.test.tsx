import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { Toaster } from 'react-hot-toast';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { RoutinesPage } from './RoutinesPage';
import { RoutineEditorPage } from './RoutineEditorPage';
import { RoutinePlayerPage } from './RoutinePlayerPage';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { plansApi } from '@/api/plans';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { MOBILITY_A_ID, MOBILITY_B_ID } from '@/mocks/fixtures/plan';
import { DoseUnit, SideBias } from '@/types/mobility';
import { SessionKind } from '@/types/plan';

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
  return { ...actual, todayLocal: vi.fn(() => '2026-11-11') };
});

vi.mock('./playerEffects', () => ({ beep: vi.fn(), primeAudio: vi.fn(), useWakeLock: vi.fn() }));

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/routines" element={<><p>Routines list</p><RoutinesPage /></>} />
          <Route path="/routines/new" element={<RoutineEditorPage />} />
          <Route path="/routines/:id/edit" element={<RoutineEditorPage />} />
          <Route path="/routines/:id/play" element={<RoutinePlayerPage />} />
          <Route path="/today" element={<p>Today page</p>} />
        </Routes>
      </MemoryRouter>
      <Toaster />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.useRealTimers();
  installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default());
  installPlansHandlers(plansHandlers.cardio());
});

describe('RoutinesPage', () => {
  it('lists routines with play and edit links', async () => {
    renderAt('/routines');

    const card = await screen.findByRole('article', { name: 'Mobility B' });
    expect(within(card).getByText(/2 drills · about \d+ min/)).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Play Mobility B' })).toHaveAttribute('href', `/routines/${MOBILITY_B_ID}/play`);
    expect(within(card).getByRole('link', { name: 'Edit Mobility B' })).toHaveAttribute('href', `/routines/${MOBILITY_B_ID}/edit`);
  });

  it('says when there are none yet', async () => {
    installMobilityRoutinesHandlers(mobilityRoutinesHandlers.empty());
    renderAt('/routines');

    expect(await screen.findByText('No routines yet')).toBeInTheDocument();
  });
});

describe('RoutineEditorPage', () => {
  it('creates a routine from the form', async () => {
    const create = vi.spyOn(mobilityRoutinesApi, 'create');
    renderAt('/routines/new');

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Evening reset' } });
    fireEvent.change(screen.getByLabelText('Drill name'), { target: { value: 'Pigeon' } });
    fireEvent.click(screen.getByLabelText('Each side'));
    fireEvent.change(screen.getByLabelText('Extra set'), { target: { value: String(SideBias.Left) } });
    fireEvent.click(screen.getByTestId('save-routine'));

    await waitFor(() => expect(create).toHaveBeenCalled());
    expect(create.mock.calls[0][0]).toEqual({
      name: 'Evening reset',
      targetMinutes: 15,
      drills: [{ name: 'Pigeon', sets: 2, dose: { unit: DoseUnit.Seconds, amount: 30 }, extraSetSide: SideBias.Left, cues: null, standingAlternative: null, perSide: true }],
    });
  });

  it('shows errors instead of saving an incomplete routine', async () => {
    const create = vi.spyOn(mobilityRoutinesApi, 'create');
    renderAt('/routines/new');

    fireEvent.click(screen.getByTestId('save-routine'));

    expect(await screen.findByText('Give the routine a name')).toBeInTheDocument();
    expect(screen.getByText('Name the drill')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('reorders drills with the up button and saves them in the new order', async () => {
    const replace = vi.spyOn(mobilityRoutinesApi, 'replaceDrills');
    renderAt(`/routines/${MOBILITY_A_ID}/edit`);

    fireEvent.click(await screen.findByRole('button', { name: 'Move drill 2 up' }));
    fireEvent.click(screen.getByTestId('save-routine'));

    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(replace.mock.calls[0][0]).toBe(MOBILITY_A_ID);
    expect(replace.mock.calls[0][1].map((d) => d.name)).toEqual(['Open books', '90/90 breathing']);
  });
});

describe('RoutineEditorPage when editing', () => {
  it('renames the routine and changes its minutes, then saves the drills', async () => {
    const update = vi.spyOn(mobilityRoutinesApi, 'update');
    const replace = vi.spyOn(mobilityRoutinesApi, 'replaceDrills');
    renderAt(`/routines/${MOBILITY_A_ID}/edit`);

    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Gym warm-up' } });
    fireEvent.change(screen.getByLabelText('Target minutes'), { target: { value: '12' } });
    fireEvent.click(screen.getByTestId('save-routine'));

    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(update).toHaveBeenCalledWith(MOBILITY_A_ID, { name: 'Gym warm-up', targetMinutes: 12 });
    expect(await screen.findByText('Routines list')).toBeInTheDocument();
  });

  it('leaves the name and minutes alone when only the drills changed', async () => {
    const update = vi.spyOn(mobilityRoutinesApi, 'update');
    const replace = vi.spyOn(mobilityRoutinesApi, 'replaceDrills');
    renderAt(`/routines/${MOBILITY_A_ID}/edit`);

    fireEvent.click(await screen.findByRole('button', { name: 'Move drill 2 up' }));
    fireEvent.click(screen.getByTestId('save-routine'));

    await waitFor(() => expect(replace).toHaveBeenCalled());
    expect(update).not.toHaveBeenCalled();
  });

  it('deletes the routine only after confirming', async () => {
    const remove = vi.spyOn(mobilityRoutinesApi, 'remove');
    renderAt(`/routines/${MOBILITY_A_ID}/edit`);

    fireEvent.click(await screen.findByTestId('delete-routine'));
    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText('Delete Mobility A?')).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Keep it' }));
    expect(remove).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('delete-routine'));
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(remove).toHaveBeenCalledWith(MOBILITY_A_ID));
    expect(await screen.findByText('Routines list')).toBeInTheDocument();
  });

  it("shows the server's reason when the routine is still in the week", async () => {
    installMobilityRoutinesHandlers({
      ...mobilityRoutinesHandlers.default(),
      remove: () =>
        Promise.reject(
          Object.assign(new Error('409'), {
            response: { status: 409, data: { detail: "'Mobility A' is in the weekly schedule of 'Cut to 85'. Take it out of the week first." } },
          })
        ),
    });
    renderAt(`/routines/${MOBILITY_A_ID}/edit`);

    fireEvent.click(await screen.findByTestId('delete-routine'));
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }));

    expect(await screen.findByText(/Take it out of the week first/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Edit Mobility A' })).toBeInTheDocument();
  });

  it('has no delete button on a new routine', () => {
    renderAt('/routines/new');

    expect(screen.queryByTestId('delete-routine')).not.toBeInTheDocument();
  });
});

describe('RoutinePlayerPage', () => {
  it('plays through the routine and marks today\'s session done with per-drill ticks', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const complete = vi.spyOn(plansApi, 'completeSession');
    renderAt(`/routines/${MOBILITY_B_ID}/play`);

    fireEvent.click(await screen.findByTestId('start-routine'));
    expect(screen.getByRole('heading', { name: 'Half-kneeling hip flexor stretch' })).toBeInTheDocument();
    expect(screen.getByText(/Left side · Set 1 of 3 · 30 seconds/)).toBeInTheDocument();

    // Skip the five hip-flexor steps, then tap Done on the cat-cow rep step once it starts.
    for (let i = 0; i < 5; i++) fireEvent.click(screen.getByRole('button', { name: 'Next step' }));
    expect(screen.getByRole('heading', { name: 'Cat-cow' })).toBeInTheDocument();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });
    fireEvent.click(screen.getByTestId('step-done'));

    expect(screen.getByRole('heading', { name: 'Routine finished' })).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('mark-routine-done'));

    await waitFor(() => expect(complete).toHaveBeenCalled());
    expect(complete.mock.calls[0][1]).toMatchObject({ kind: SessionKind.Mobility, mobilityRoutineId: MOBILITY_B_ID, drillTicks: [false, true] });
    vi.useRealTimers();
  });

  it('counts a hold down and moves to the other side by itself', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: false });
    renderAt(`/routines/${MOBILITY_B_ID}/play`);
    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    fireEvent.click(screen.getByTestId('start-routine'));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000 + 30000);
    });

    expect(screen.getByText(/Right side · Set 1 of 2/)).toBeInTheDocument();
    vi.useRealTimers();
  });
});
