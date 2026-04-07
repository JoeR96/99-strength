import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { type ReactNode } from 'react';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'TestUser' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
}));

vi.mock('@/contexts/HevyContext', () => ({
  useHevy: vi.fn(),
  HevyProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

vi.mock('./HevyExerciseModal', () => ({
  HevyExerciseModal: () => null,
}));

vi.mock('@/components/hevy/HevySettings', () => ({
  HevySettings: () => <div data-testid="hevy-settings">HevySettings</div>,
}));

import { useHevy } from '@/contexts/HevyContext';
import { HevyDataPage } from './HevyDataPage';

function renderHevyData() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <HevyDataPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('HevyDataPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows API key prompt when not configured', () => {
    vi.mocked(useHevy).mockReturnValue({
      apiKey: null,
      isConfigured: false,
      isValidating: false,
      isValid: null,
      setApiKey: vi.fn(),
      clearApiKey: vi.fn(),
      validateKey: vi.fn(),
    });

    renderHevyData();
    expect(screen.getByText(/Hevy Data/i)).toBeInTheDocument();
  });

  it('renders page content when API key is configured', () => {
    vi.mocked(useHevy).mockReturnValue({
      apiKey: 'test-key',
      isConfigured: true,
      isValidating: false,
      isValid: true,
      setApiKey: vi.fn(),
      clearApiKey: vi.fn(),
      validateKey: vi.fn(),
    });

    renderHevyData();
    expect(screen.getByText(/Hevy Data/i)).toBeInTheDocument();
  });
});
