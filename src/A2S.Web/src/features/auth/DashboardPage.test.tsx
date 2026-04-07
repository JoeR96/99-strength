import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { type ReactNode } from 'react';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'TestUser' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  SignedIn: ({ children }: { children: ReactNode }) => <>{children}</>,
  SignedOut: () => null,
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock('@/hooks/useWorkouts', () => ({
  useCurrentWorkout: vi.fn(),
}));

vi.mock('@/features/workout/WeekOverview', () => ({
  WeekOverview: () => <div data-testid="week-overview">WeekOverview</div>,
}));

vi.mock('@/features/workout/NextWeekPreview', () => ({
  NextWeekPreview: () => <div data-testid="next-week-preview">NextWeekPreview</div>,
}));

vi.mock('@/features/auth/DashboardExerciseTracking', () => ({
  DashboardExerciseTracking: () => <div data-testid="exercise-tracking">ExerciseTracking</div>,
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { DashboardPage } from './DashboardPage';

function renderDashboard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('DashboardPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders welcome message with user name', () => {
    vi.mocked(useCurrentWorkout).mockReturnValue({
      data: null,
      isLoading: false,
      refetch: vi.fn(),
    } as any);

    renderDashboard();
    expect(screen.getByText(/Welcome back, TestUser/)).toBeInTheDocument();
  });

  it('shows loading state', () => {
    vi.mocked(useCurrentWorkout).mockReturnValue({
      data: undefined,
      isLoading: true,
      refetch: vi.fn(),
    } as any);

    renderDashboard();
    expect(screen.getByText(/Welcome back/)).toBeInTheDocument();
  });

  it('shows start program link when no active workout', () => {
    vi.mocked(useCurrentWorkout).mockReturnValue({
      data: null,
      isLoading: false,
      refetch: vi.fn(),
    } as any);

    renderDashboard();
    expect(screen.getByText(/Start Program/i)).toBeInTheDocument();
  });

  it('shows quick stats when workout exists', () => {
    vi.mocked(useCurrentWorkout).mockReturnValue({
      data: {
        id: 'w-1',
        name: 'Test Program',
        variant: 4,
        status: 2,
        currentWeek: 3,
        currentBlock: 1,
        currentDay: 2,
        daysPerWeek: 4,
        completedDaysInCurrentWeek: [1],
        isWeekComplete: false,
        totalWeeks: 21,
        startDate: '2024-01-01',
        createdAt: '2024-01-01',
        exerciseCount: 8,
        exercises: [],
      },
      isLoading: false,
      refetch: vi.fn(),
    } as any);

    renderDashboard();
    expect(screen.getByText('Quick Stats')).toBeInTheDocument();
    expect(screen.getByText('1/4')).toBeInTheDocument();
  });
});
