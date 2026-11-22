import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { TracksPage } from './TracksPage';
import { TrackLogSheet } from './TrackLogSheet';
import { CardioTrackChart } from './CardioTrackProgress';
import { TodayCardioTracks } from './TodayCardioTracks';
import { stepUpText } from './trackText';
import { cardioTracksApi } from '@/api/cardioTracks';
import { cardioTracksHandlers, installCardioTracksHandlers } from '@/mocks/handlers/cardioTracks';
import { RUN_TRACK_ID, STAIR_TRACK_ID, WALK_TRACK_ID, runTrack, stairDetailFixture, stairTrack, walkTrack } from '@/mocks/fixtures/cardioTracks';
import { CardioTrackStatus, EffortRating, IntensityUnit, PainFlag } from '@/types/cardioTracks';
import { OfflineContext } from '@/offline/context';
import { createQueue } from '@/offline/queue';
import { createSyncManager } from '@/offline/syncManager';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'Joe' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  UserButton: () => <div data-testid="user-button" />,
  SignedIn: ({ children }: { children: ReactNode }) => <>{children}</>,
  SignedOut: () => null,
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

function renderWithProviders(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  installCardioTracksHandlers(cardioTracksHandlers.default());
});

describe('TracksPage', () => {
  it('shows each track with its prescription and sessions to the next step', async () => {
    renderWithProviders(<TracksPage />);

    const walk = await screen.findByRole('article', { name: 'Walk builder' });
    expect(within(walk).getByTestId('prescription')).toHaveTextContent('25 min continuous');
    expect(within(walk).getByText('1 more session to step up')).toBeInTheDocument();
    const stairs = screen.getByRole('article', { name: 'Stair finisher' });
    expect(within(stairs).getByText('Zone 4–5')).toBeInTheDocument();
    expect(within(stairs).getByRole('button', { name: 'Undo last change to Stair finisher' })).toBeDisabled();
  });

  it('accepts a suggestion', async () => {
    const decide = vi.spyOn(cardioTracksApi, 'decide');
    renderWithProviders(<TracksPage />);

    const run = await screen.findByRole('article', { name: 'Walk → run' });
    expect(within(run).getByText('Not every session this week counted. Repeat the week?')).toBeInTheDocument();
    fireEvent.click(within(run).getByRole('button', { name: 'Yes' }));

    await waitFor(() => expect(decide).toHaveBeenCalledWith(RUN_TRACK_ID, true));
  });

  it('undoes, pauses and starts from a template', async () => {
    const undo = vi.spyOn(cardioTracksApi, 'undo');
    const pause = vi.spyOn(cardioTracksApi, 'pause');
    const create = vi.spyOn(cardioTracksApi, 'create');
    renderWithProviders(<TracksPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Undo last change to Walk builder' }));
    fireEvent.click(screen.getByRole('button', { name: 'Pause Walk builder' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Start Stair finisher' }));

    await waitFor(() => expect(undo).toHaveBeenCalledWith(WALK_TRACK_ID));
    expect(pause).toHaveBeenCalledWith(WALK_TRACK_ID);
    await waitFor(() => expect(create).toHaveBeenCalledWith('stair-finisher', undefined));
  });

  it('logs a session with one effort tap', async () => {
    const logSession = vi.spyOn(cardioTracksApi, 'logSession');
    renderWithProviders(<TracksPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Log session: Walk builder' }));
    fireEvent.click(await screen.findByRole('button', { name: /^Easy/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Log session' }));

    await waitFor(() => expect(logSession).toHaveBeenCalled());
    const [id, request] = logSession.mock.calls[0];
    expect(id).toBe(WALK_TRACK_ID);
    expect(request.log.effort).toBe(EffortRating.Easy);
    expect(request.log.durationSeconds).toBe(25 * 60);
    expect(request.completedAsPrescribed).toBe(true);
    expect(request.clientKey).toBeTruthy();
  });

  it('edits a stage in the info sheet, which shows the safety line', async () => {
    const update = vi.spyOn(cardioTracksApi, 'update');
    renderWithProviders(<TracksPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Info about Walk builder' }));
    expect(await screen.findByText(/stop for chest pain/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Up to', { selector: '#stage-0-max' }), { target: { value: '40' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(update).toHaveBeenCalled());
    expect(update.mock.calls[0][1].stages[0].doubleProgression?.volumeMax).toBe(40);
  });

  it('shows an error with a retry', async () => {
    installCardioTracksHandlers(cardioTracksHandlers.error());
    renderWithProviders(<TracksPage />);

    expect(await screen.findByText("Couldn't load your tracks.")).toBeInTheDocument();
  });
});

describe('TrackLogSheet', () => {
  it('needs an effort before logging', () => {
    const onSubmit = vi.fn();
    renderWithProviders(<TrackLogSheet track={walkTrack} onClose={vi.fn()} onSubmit={onSubmit} />);

    fireEvent.click(screen.getByRole('button', { name: 'Log session' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Pick Easy, Solid or Brutal');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('sends optional details, the level and a pain flag', () => {
    const onSubmit = vi.fn();
    renderWithProviders(<TrackLogSheet track={stairTrack} onClose={vi.fn()} onSubmit={onSubmit} initiallyExpanded />);

    fireEvent.click(screen.getByRole('button', { name: /^Brutal/ }));
    fireEvent.change(screen.getByLabelText('Minutes'), { target: { value: '7' } });
    fireEvent.change(screen.getByLabelText('Level'), { target: { value: '7' } });
    fireEvent.change(screen.getByLabelText('Floors'), { target: { value: '40' } });
    fireEvent.change(screen.getByLabelText('Zone 4–5 min'), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Had to stop' }));
    fireEvent.change(screen.getByLabelText('Where?'), { target: { value: 'Left knee' } });
    fireEvent.click(screen.getByRole('button', { name: 'Log session' }));

    const request = onSubmit.mock.calls[0][1];
    expect(request.completedAsPrescribed).toBe(false);
    expect(request.log).toMatchObject({
      durationSeconds: 420,
      effort: EffortRating.Brutal,
      intensityUnit: IntensityUnit.Level,
      intensityValue: 7,
      floors: 40,
      highIntensityMinutes: 5,
      pain: PainFlag.Stop,
      painArea: 'Left knee',
    });
  });
});

describe('CardioTrackChart', () => {
  it('lists each change and why, and offers the data as a table', () => {
    renderWithProviders(<CardioTrackChart detail={stairDetailFixture} />);

    expect(screen.getByText('6 min continuous @ level 6 → 7 min continuous @ level 6')).toBeInTheDocument();
    expect(screen.getByText('Not enough time in zone 4–5: holding')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Stair finisher sessions' })).toBeInTheDocument();
    expect(screen.getByText(/Floors in 5 min, all out: best 30 floors/)).toBeInTheDocument();
  });
});

describe('offline and drafts', () => {
  let store: Map<string, string>;
  beforeEach(() => {
    store = new Map<string, string>();
    vi.mocked(localStorage.getItem).mockImplementation((key: string) => store.get(key) ?? null);
    vi.mocked(localStorage.setItem).mockImplementation((key: string, value: string) => void store.set(key, value));
    vi.mocked(localStorage.removeItem).mockImplementation((key: string) => void store.delete(key));
  });

  it('queues a log offline with its client key and shows the track as waiting to sync', async () => {
    const logSession = vi.spyOn(cardioTracksApi, 'logSession');
    const queue = createQueue(`tracks-${Math.random().toString(36).slice(2)}`);
    const manager = createSyncManager({ queue, isOnline: () => false });
    renderWithProviders(
      <OfflineContext.Provider value={manager}>
        <TracksPage />
      </OfflineContext.Provider>
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Log session: Walk builder' }));
    fireEvent.click(await screen.findByRole('button', { name: /^Easy/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Log session' }));

    const walk = await screen.findByRole('article', { name: 'Walk builder' });
    await waitFor(() => expect(within(walk).getByText('Waiting to sync')).toBeInTheDocument());
    expect(logSession).not.toHaveBeenCalled();
    const [op] = await queue.all();
    expect(op.kind).toBe('cardio.log');
    expect(op.target).toEqual({ trackId: WALK_TRACK_ID });
    expect((op.payload as { clientKey: string }).clientKey).toMatch(/^[0-9a-f-]{36}$/);
    expect(manager.getPendingCount()).toBe(1);
    await queue.close();
  });

  it('keeps an unsaved info-sheet edit and offers to restore it when the sheet opens again', async () => {
    const update = vi.spyOn(cardioTracksApi, 'update');
    renderWithProviders(<TracksPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Info about Walk builder' }));
    fireEvent.change(await screen.findByLabelText('Up to', { selector: '#stage-0-max' }), { target: { value: '41' } });
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Info about Walk builder' }));
    expect(await screen.findByRole('heading', { name: 'Restore unsaved changes?' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(screen.getByLabelText('Up to', { selector: '#stage-0-max' })).toHaveValue('41');
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(update).toHaveBeenCalled());
    expect(update.mock.calls.map((c) => c[1].stages[0].doubleProgression?.volumeMax)).toEqual([41]);
    await waitFor(() => expect([...store.keys()].filter((k) => k.startsWith('a2s-draft:'))).toEqual([]));
  });
});

describe('TodayCardioTracks', () => {
  it('shows prescriptions and the zone 4–5 warning', async () => {
    renderWithProviders(<TodayCardioTracks />);

    expect(await screen.findByText(/above your 30 min max/)).toBeInTheDocument();
    expect(screen.getByRole('article', { name: 'Stair finisher' })).toHaveTextContent('2 more sessions to step up');
    expect(screen.queryByRole('button', { name: /^Pause/ })).not.toBeInTheDocument();
  });

  it('renders nothing without tracks', async () => {
    installCardioTracksHandlers(cardioTracksHandlers.empty());
    const { container } = renderWithProviders(<TodayCardioTracks />);

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });
});

describe('stepUpText', () => {
  it('covers paused, waiting and counting', () => {
    expect(stepUpText({ ...walkTrack, status: CardioTrackStatus.Paused })).toMatch(/^Paused/);
    expect(stepUpText(runTrack)).toBe('Waiting for your decision');
    expect(stepUpText({ ...stairTrack, id: STAIR_TRACK_ID })).toBe('2 more sessions to step up');
  });
});
