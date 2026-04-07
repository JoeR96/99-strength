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
  useSetActiveWorkout: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useDeleteWorkout: vi.fn(() => ({ mutateAsync: vi.fn() })),
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

import { useAllWorkouts } from '@/hooks/useWorkouts';
import { ProgramsPage } from './ProgramsPage';

function renderProgramsPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ProgramsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('ProgramsPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows loading state', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any);

    renderProgramsPage();
    expect(screen.getByText(/Loading programs/)).toBeInTheDocument();
  });

  it('shows error state', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as any);

    renderProgramsPage();
    expect(screen.getByText(/Failed to load programs/)).toBeInTheDocument();
    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('renders programs list', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: [
        {
          id: 'w-1',
          name: 'Program A',
          variant: '4',
          status: 'Active',
          currentWeek: 3,
          currentBlock: 1,
          currentDay: 2,
          daysPerWeek: 4,
          completedDaysInCurrentWeek: [],
          isWeekComplete: false,
          totalWeeks: 21,
          createdAt: '2024-01-01',
          exerciseCount: 8,
          isActive: true,
        },
        {
          id: 'w-2',
          name: 'Program B',
          variant: '5',
          status: 'Inactive',
          currentWeek: 1,
          currentBlock: 1,
          currentDay: 1,
          daysPerWeek: 5,
          completedDaysInCurrentWeek: [],
          isWeekComplete: false,
          totalWeeks: 21,
          createdAt: '2024-02-01',
          exerciseCount: 10,
          isActive: false,
        },
      ],
      isLoading: false,
      error: null,
    } as any);

    renderProgramsPage();
    expect(screen.getByText('Program A')).toBeInTheDocument();
    expect(screen.getByText('Program B')).toBeInTheDocument();
  });

  it('shows empty state when no programs', () => {
    vi.mocked(useAllWorkouts).mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as any);

    renderProgramsPage();
    expect(screen.getByText(/No Programs/i)).toBeInTheDocument();
  });
});
