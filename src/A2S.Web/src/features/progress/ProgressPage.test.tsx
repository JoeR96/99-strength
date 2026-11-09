import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { ProgressPage } from './ProgressPage';
import { WeightTrendChart } from './WeightTrendChart';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installBudgetHandlers, budgetHandlers } from '@/mocks/handlers/budget';
import { installWorkoutsHandlers, workoutsHandlers } from '@/mocks/handlers/workouts';

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
  return { ...actual, todayLocal: vi.fn(() => '2026-12-10') };
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/progress']}>
        <ProgressPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('ProgressPage', () => {
  beforeEach(() => {
    installPlansHandlers(plansHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.onPace());
    installBudgetHandlers(budgetHandlers.default());
    installWorkoutsHandlers(workoutsHandlers.default());
  });

  it('shows the pace label, volume bars with the warning, checkpoints and the history link', async () => {
    renderPage();
    const pace = await screen.findByTestId('pace-label');
    expect(within(pace).getByText('On pace')).toHaveAttribute('data-tone', 'success');
    expect(await screen.findByTestId('volume-Back')).toHaveTextContent('16/14');
    expect(screen.getByLabelText('Volume warnings')).toHaveTextContent(/over the cap/i);
    expect(await screen.findByTestId('checkpoints')).toBeInTheDocument();
    expect(screen.getByTestId('checkpoint-2026-12-07')).toHaveTextContent('92 kg');
    expect(screen.getByRole('link', { name: /workout history/i })).toHaveAttribute('href', '/history');
  });

  it('Behind is amber with the delta, Ahead is primary', async () => {
    installBodyWeightHandlers(bodyWeightHandlers.behind());
    const { unmount } = renderPage();
    let pace = await screen.findByTestId('pace-label');
    expect(within(pace).getByText('Behind')).toHaveAttribute('data-tone', 'warning');
    expect(pace).toHaveTextContent(/\+0\.9 kg vs target/);
    unmount();

    installBodyWeightHandlers(bodyWeightHandlers.ahead());
    renderPage();
    pace = await screen.findByTestId('pace-label');
    expect(within(pace).getByText('Ahead')).toHaveAttribute('data-tone', 'primary');
  });

  it('with no plan shows No target and no checkpoints, but still the trend', async () => {
    installPlansHandlers(plansHandlers.noPlan());
    installBodyWeightHandlers(bodyWeightHandlers.noTarget());
    installWorkoutsHandlers(workoutsHandlers.none());
    renderPage();
    const pace = await screen.findByTestId('pace-label');
    expect(within(pace).getByText('No target')).toBeInTheDocument();
    expect(screen.queryByTestId('checkpoints')).not.toBeInTheDocument();
    expect(await screen.findByText(/no lifting programme yet/i)).toBeInTheDocument();
  });

  it('renders the empty state with zero points without crashing', async () => {
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    renderPage();
    expect(await screen.findByTestId('trend-empty')).toBeInTheDocument();
  });
});

describe('WeightTrendChart', () => {
  it('renders with no trend at all', () => {
    render(<WeightTrendChart trend={undefined} />);
    expect(screen.getByTestId('trend-empty')).toBeInTheDocument();
    expect(screen.getByText('No target')).toBeInTheDocument();
  });

  it('renders the chart container with points', () => {
    render(
      <WeightTrendChart
        trend={{ points: [{ date: '2026-11-09', kg: 95, movingAverage7: null, ewma: 95 }], pace: null }}
        checkpoints={[{ date: '2026-12-07', targetKg: 92, note: null }]}
        startDate="2026-11-09"
        startKg={95}
      />
    );
    expect(screen.getByTestId('trend-chart')).toBeInTheDocument();
    expect(screen.queryByText(/7-day average/)).not.toBeInTheDocument();
  });
});
