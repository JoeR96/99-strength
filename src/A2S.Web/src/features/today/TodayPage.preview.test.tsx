import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { TodayPage } from './TodayPage';
import { plansApi } from '@/api/plans';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import {
  installSuggestedPlansHandlers,
  suggestedPlansHandlers,
} from '@/mocks/handlers/suggestedPlans';
import { dayFromSchedule } from '@/mocks/fixtures/plan';
import { todayLocal } from '@/lib/localDate';

vi.mock('@/components/layout/Navbar', () => ({ Navbar: () => null }));

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

function Where() {
  const location = useLocation();
  return <p data-testid="location">{location.pathname + location.search}</p>;
}

function renderToday(path = '/today') {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  const page = (children: ReactNode) => (
    <>
      {children}
      <Where />
    </>
  );
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/today" element={page(<TodayPage />)} />
          <Route path="/gym/:day" element={page(null)} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('TodayPage looking ahead', () => {
  beforeEach(() => {
    vi.mocked(todayLocal).mockReturnValue('2026-11-09');
    installPlansHandlers(plansHandlers.week());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    installSuggestedPlansHandlers(suggestedPlansHandlers.default());
  });

  it('steps to the next day read-only, keeps it in the URL, and comes back to today', async () => {
    const getToday = vi.spyOn(plansApi, 'getToday');
    renderToday();
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Next day' }));

    await waitFor(() => expect(screen.getByTestId('today-date')).toHaveTextContent('Tue 10 Nov'));
    expect(screen.getByTestId('location')).toHaveTextContent('/today?date=2026-11-10');
    expect(getToday).toHaveBeenCalledWith('2026-11-10');
    expect(await screen.findByTestId('preview-session')).toBeInTheDocument();
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Habits' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('back-to-today'));

    await waitFor(() => expect(screen.getByTestId('today-date')).toHaveTextContent('Mon 9 Nov'));
    expect(screen.queryByTestId('back-to-today')).not.toBeInTheDocument();
  });

  it('opens a linked future day and previews its lift in gym mode', async () => {
    renderToday('/today?date=2026-11-12');

    fireEvent.click(await screen.findByTestId('preview-session'));

    expect(screen.getByTestId('location')).toHaveTextContent('/gym/3?preview=1&date=2026-11-12');
  });

  it('past days are read-only too', async () => {
    renderToday('/today?date=2026-11-02');

    expect(await screen.findByTestId('preview-session')).toBeInTheDocument();
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
    expect(screen.getByText('Past day')).toBeInTheDocument();
  });

  it('on a rest day shows Up next: tomorrow with its main lift and top set', async () => {
    installPlansHandlers({
      ...plansHandlers.week(),
      getToday: async (date) => dayFromSchedule(date ?? '2026-11-15'),
    });
    const getUpcoming = vi.spyOn(plansApi, 'getUpcoming');
    vi.mocked(todayLocal).mockReturnValue('2026-11-15');
    renderToday();

    const upNext = await screen.findByTestId('up-next');
    expect(upNext).toHaveTextContent('Tomorrow · Mon 16 Nov');
    expect(upNext).toHaveTextContent('Upper A');
    expect(await screen.findByTestId('session-summary')).toHaveTextContent(
      '2 exercises · Bench Press (Barbell) 5×5 @ 75 kg'
    );
    expect(getUpcoming).toHaveBeenCalledWith('2026-11-16', 14);

    fireEvent.click(screen.getByTestId('up-next-preview'));
    expect(screen.getByTestId('location')).toHaveTextContent('/gym/1?preview=1&date=2026-11-16');
  });

  it('says so when nothing is planned in the next 14 days, and shows no card without a plan', async () => {
    installPlansHandlers({
      ...plansHandlers.rest(),
      getUpcoming: async (from, days = 14) =>
        Array.from({ length: days }, (_, i) => ({
          ...dayFromSchedule('2026-11-15'),
          date: `${from}-${i}`,
          lifts: [],
        })),
    });
    const { unmount } = renderToday();
    expect(await screen.findByTestId('up-next')).toHaveTextContent(
      'Nothing planned in the next two weeks.'
    );
    unmount();

    installPlansHandlers(plansHandlers.noPlan());
    renderToday();
    await waitFor(() => expect(screen.queryByTestId('today-date')).toBeInTheDocument());
    await new Promise((r) => setTimeout(r, 20));
    expect(screen.queryByTestId('up-next')).not.toBeInTheDocument();
  });

  it('does not show Up next while today still has a session to do', async () => {
    renderToday();
    expect(await screen.findByTestId('start-session')).toBeInTheDocument();
    expect(screen.queryByTestId('up-next')).not.toBeInTheDocument();
  });
});
