import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { type ReactNode } from 'react';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'TestUser' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('@/api', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('@/data/workoutTemplates', () => ({
  workoutTemplates: [],
}));

import { SettingsPage } from './SettingsPage';

function renderSettings() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('SettingsPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the settings page', () => {
    renderSettings();
    const headings = screen.getAllByText(/Settings/i);
    expect(headings.length).toBeGreaterThan(0);
  });

  it('shows seed data button', () => {
    renderSettings();
    expect(screen.getByText(/Seed.*Data/i)).toBeInTheDocument();
  });

  it('shows export button', () => {
    renderSettings();
    const exportElements = screen.getAllByText(/Export/i);
    expect(exportElements.length).toBeGreaterThan(0);
  });
});
