import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { type ReactNode } from 'react';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'TestUser' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
}));

vi.mock('@/hooks/useWorkouts', () => ({
  useAllWorkouts: vi.fn(),
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('recharts', () => ({
  LineChart: ({ children }: { children: ReactNode }) => <div data-testid="line-chart">{children}</div>,
  Line: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  ResponsiveContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Legend: () => null,
}));

import { useAllWorkouts } from '@/hooks/useWorkouts';
import { SimulationPage } from './SimulationPage';

function renderSimulation() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SimulationPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('SimulationPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the simulation page with controls', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: [
        {
          id: 'w-1',
          name: 'Test Program',
          variant: '4',
          status: 'Active',
          currentWeek: 1,
          currentBlock: 1,
          currentDay: 1,
          daysPerWeek: 4,
          completedDaysInCurrentWeek: [],
          isWeekComplete: false,
          totalWeeks: 21,
          createdAt: '2024-01-01',
          exerciseCount: 8,
          isActive: true,
        },
      ],
      isLoading: false,
      error: null,
    } as any);

    renderSimulation();
    expect(screen.getByText(/Workout Simulator/i)).toBeInTheDocument();
  });

  it('shows loading when workouts are loading', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    renderSimulation();
    expect(screen.getByText(/Loading/i)).toBeInTheDocument();
  });
});
