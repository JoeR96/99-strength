import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';

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

vi.mock('@/api/apiTokens', () => ({
  apiTokensApi: { list: vi.fn().mockResolvedValue([]), create: vi.fn(), revoke: vi.fn() },
}));

vi.mock('@/contexts/HevyContext', () => ({
  useHevy: () => ({
    isConfigured: true,
    keyLastFour: '7781',
    isValidating: false,
    isValid: true,
    isLoading: false,
    setApiKey: vi.fn(),
    clearApiKey: vi.fn(),
    validateKey: vi.fn(),
  }),
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

  it('no longer offers the destructive seed-data action', () => {
    renderSettings();
    expect(screen.queryByText(/Seed.*Data/i)).not.toBeInTheDocument();
  });

  it('shows Hevy key management with only the last four characters', () => {
    renderSettings();
    expect(screen.getByText(/Hevy Integration/i)).toBeInTheDocument();
    expect(screen.getByTestId('hevy-key-masked')).toHaveTextContent('7781');
    expect(screen.getByText(/Replace key/i)).toBeInTheDocument();
    expect(screen.getByText(/Remove key/i)).toBeInTheDocument();
  });

  it('has the desktop widget tokens section', async () => {
    renderSettings();
    expect(screen.getByText('Desktop widget tokens')).toBeInTheDocument();
    expect(await screen.findByTestId('api-tokens-empty')).toBeInTheDocument();
  });

  describe('the Simulator link', () => {
    afterEach(() => vi.unstubAllEnvs());

    it('is hidden in a production build, where the API has no simulate route', () => {
      vi.stubEnv('DEV', false);
      renderSettings();
      expect(screen.getByRole('link', { name: /Exercise library/ })).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: /Simulator/ })).not.toBeInTheDocument();
    });

    it('is offered in development', () => {
      vi.stubEnv('DEV', true);
      renderSettings();
      expect(screen.getByRole('link', { name: /Simulator/ })).toBeInTheDocument();
    });
  });

  it('shows export button', () => {
    renderSettings();
    const exportElements = screen.getAllByText(/Export/i);
    expect(exportElements.length).toBeGreaterThan(0);
  });
});
