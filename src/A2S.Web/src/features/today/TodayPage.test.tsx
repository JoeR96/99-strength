import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { TodayPage } from './TodayPage';
import { plansApi } from '@/api/plans';
import { habitsApi } from '@/api/habits';
import { bodyWeightApi } from '@/api/bodyWeight';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { WATER_ID } from '@/mocks/fixtures/habits';
import { installSuggestedPlansHandlers, suggestedPlansHandlers } from '@/mocks/handlers/suggestedPlans';
import { suggestedPlansApi } from '@/api/suggestedPlans';
import { LONG_PRESS_MS } from './HabitCounter';

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
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

function renderToday() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/today']}>
        <TodayPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('TodayPage', () => {
  beforeEach(() => {
    installPlansHandlers(plansHandlers.default());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    installSuggestedPlansHandlers(suggestedPlansHandlers.default());
  });

  it('logs a bottle of water with one tap from mount', async () => {
    const incrementSpy = vi.spyOn(habitsApi, 'increment');
    renderToday();
    const water = await screen.findByTestId(`habit-counter-${WATER_ID}`);
    expect(within(water).getByText('2')).toBeInTheDocument();

    fireEvent.click(within(water).getByRole('button', { name: /add one water/i }));

    await waitFor(() => expect(within(water).getByText('3')).toBeInTheDocument());
    expect(incrementSpy).toHaveBeenCalledTimes(1);
    expect(incrementSpy).toHaveBeenCalledWith(
      WATER_ID,
      '2026-11-09',
      expect.objectContaining({ delta: 1, clientTimestamp: expect.any(String) })
    );
  });

  it('holding + for 500 ms takes one off instead', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const incrementSpy = vi.spyOn(habitsApi, 'increment');
      renderToday();
      const water = await screen.findByTestId(`habit-counter-${WATER_ID}`);
      const plus = within(water).getByRole('button', { name: /add one water/i });

      fireEvent.pointerDown(plus, { button: 0, pointerType: 'touch' });
      await act(async () => {
        vi.advanceTimersByTime(LONG_PRESS_MS + 10);
      });
      fireEvent.pointerUp(plus);
      fireEvent.click(plus);

      await waitFor(() => expect(incrementSpy).toHaveBeenCalledTimes(1));
      expect(incrementSpy.mock.calls[0][2]).toMatchObject({ delta: -1 });
      await waitFor(() => expect(within(water).getByText('1')).toBeInTheDocument());
    } finally {
      vi.useRealTimers();
    }
  });

  it('shows Start for a planned lift', async () => {
    renderToday();
    const start = await screen.findByTestId('start-session');
    expect(start).toHaveAttribute('href', '/workout/session/1');
  });

  it('on a paused day shows the banner and no Start button', async () => {
    installPlansHandlers(plansHandlers.paused());
    renderToday();
    expect(await screen.findByTestId('phase-banner')).toHaveTextContent(/paused/i);
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
    expect(screen.queryByTestId('mark-done')).not.toBeInTheDocument();
    expect(screen.getByTestId('ring-lifts')).toHaveTextContent('—');
  });

  it('with no plan shows the Start this plan card but still habits and weigh-in', async () => {
    installPlansHandlers(plansHandlers.noPlan());
    renderToday();
    const card = await screen.findByTestId('start-suggested-plan-card');
    expect(within(card).getByRole('button', { name: /start this plan/i })).toBeInTheDocument();
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
    expect(await screen.findByTestId(`habit-counter-${WATER_ID}`)).toBeInTheDocument();
    expect(screen.getByTestId('weigh-in')).toBeInTheDocument();
  });

  it('after starting the suggested plan, Today refetches and shows the session', async () => {
    installPlansHandlers(plansHandlers.noPlan());
    const startSpy = vi.spyOn(suggestedPlansApi, 'startCutTo85');
    renderToday();
    const card = await screen.findByTestId('start-suggested-plan-card');
    fireEvent.change(within(card).getByLabelText('Bench press TM'), { target: { value: '100' } });
    fireEvent.change(within(card).getByLabelText('Squat TM'), { target: { value: '140' } });
    fireEvent.change(within(card).getByLabelText('Overhead press TM'), { target: { value: '60' } });
    fireEvent.change(within(card).getByLabelText('Deadlift TM'), { target: { value: '180' } });
    // The server now has a plan: later reads return Monday's session.
    installPlansHandlers(plansHandlers.default());
    fireEvent.click(within(card).getByRole('button', { name: /start this plan/i }));
    await waitFor(() => expect(startSpy).toHaveBeenCalledTimes(1));
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(screen.queryByTestId('start-suggested-plan-card')).not.toBeInTheDocument();
  });

  it('marks a cardio session done through the log sheet', async () => {
    installPlansHandlers(plansHandlers.cardio());
    const completeSpy = vi.spyOn(plansApi, 'completeSession');
    renderToday();
    const buttons = await screen.findAllByTestId('mark-done');
    fireEvent.click(buttons[0]);
    const minutes = await screen.findByLabelText(/^minutes$/i);
    fireEvent.change(minutes, { target: { value: '38' } });
    fireEvent.click(screen.getByRole('button', { name: /^mark done$/i }));
    await waitFor(() => expect(completeSpy).toHaveBeenCalledTimes(1));
    expect(completeSpy.mock.calls[0][1]).toMatchObject({
      date: '2026-11-09',
      kind: 2,
      cardio: expect.objectContaining({ durationSeconds: 38 * 60, highIntensityMinutes: null }),
    });
  });

  it('sends zone 4–5 minutes when typed into the cardio sheet', async () => {
    installPlansHandlers(plansHandlers.cardio());
    const completeSpy = vi.spyOn(plansApi, 'completeSession');
    renderToday();
    fireEvent.click((await screen.findAllByTestId('mark-done'))[0]);
    fireEvent.change(await screen.findByLabelText(/^minutes$/i), { target: { value: '38' } });
    const zone = screen.getByLabelText(/zone 4–5 minutes/i);
    expect(zone).toHaveAttribute('inputmode', 'numeric');
    fireEvent.change(zone, { target: { value: '16' } });
    fireEvent.click(screen.getByRole('button', { name: /^mark done$/i }));
    await waitFor(() => expect(completeSpy).toHaveBeenCalledTimes(1));
    expect(completeSpy.mock.calls[0][1]).toMatchObject({ cardio: expect.objectContaining({ highIntensityMinutes: 16 }) });
  });

  it('saves a weigh-in to the body-weight API', async () => {
    const recordSpy = vi.spyOn(bodyWeightApi, 'record');
    renderToday();
    await screen.findByTestId(`habit-counter-${WATER_ID}`);
    const field = screen.getByTestId('weigh-in');
    // The fixture already has 94.6 logged on the numeric habit, so it shows the value with Edit.
    expect(within(field).getByText('94.6')).toBeInTheDocument();
    fireEvent.click(within(field).getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText(/morning weigh-in/i), { target: { value: '94.2' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    await waitFor(() => expect(recordSpy).toHaveBeenCalledWith('2026-11-09', 94.2));
  });

  it('shows an error card with retry when the plan fails to load', async () => {
    installPlansHandlers(plansHandlers.error());
    renderToday();
    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn't load today's plan/i);
  });
});
