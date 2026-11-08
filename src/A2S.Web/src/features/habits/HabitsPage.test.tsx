import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { HabitsPage } from './HabitsPage';
import { habitsApi } from '@/api/habits';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { WATER_ID } from '@/mocks/fixtures/habits';

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

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/habits']}>
        <HabitsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('HabitsPage', () => {
  beforeEach(() => {
    installHabitsHandlers(habitsHandlers.default());
  });

  it('lists today’s habits with the streak and a heatmap', async () => {
    renderPage();
    expect(await screen.findByTestId(`habit-row-${WATER_ID}`)).toBeInTheDocument();
    expect(screen.getByTestId('streak-chip')).toHaveTextContent('5');
    expect(await screen.findByTestId('habit-heatmap')).toBeInTheDocument();
  });

  it('archiving a habit removes it from the today list', async () => {
    const archiveSpy = vi.spyOn(habitsApi, 'archive');
    renderPage();
    const row = await screen.findByTestId(`habit-row-${WATER_ID}`);
    fireEvent.click(within(row).getByRole('button', { name: /edit water/i }));
    fireEvent.click(await screen.findByRole('button', { name: /^archive$/i }));
    fireEvent.click(screen.getByRole('button', { name: /confirm archive/i }));
    await waitFor(() => expect(archiveSpy).toHaveBeenCalledWith(WATER_ID));
    await waitFor(() => expect(screen.queryByTestId(`habit-row-${WATER_ID}`)).not.toBeInTheDocument());
    expect(screen.getByText(/archived \(1\)/i)).toBeInTheDocument();
  });

  it('a + on a counter row sends an increment', async () => {
    const incrementSpy = vi.spyOn(habitsApi, 'increment');
    renderPage();
    const row = await screen.findByTestId(`habit-row-${WATER_ID}`);
    fireEvent.click(within(row).getByRole('button', { name: /add one water/i }));
    await waitFor(() => expect(incrementSpy).toHaveBeenCalledWith(WATER_ID, '2026-11-09', expect.objectContaining({ delta: 1 })));
  });

  it('creates a habit from the sheet', async () => {
    const createSpy = vi.spyOn(habitsApi, 'create');
    renderPage();
    await screen.findByTestId(`habit-row-${WATER_ID}`);
    fireEvent.click(screen.getByTestId('new-habit'));
    fireEvent.change(await screen.findByLabelText(/^name$/i), { target: { value: 'Reading' } });
    fireEvent.change(screen.getByLabelText(/^target$/i), { target: { value: "20" } });
    fireEvent.change(screen.getByLabelText(/^minimum$/i), { target: { value: "5" } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    await waitFor(() => expect(createSpy).toHaveBeenCalledWith(expect.objectContaining({ name: 'Reading', target: 20, minimum: 5, order: 6 })));
    expect((await screen.findAllByText('Reading')).length).toBeGreaterThan(0);
  });

  it('shows the empty state when there are no habits', async () => {
    installHabitsHandlers(habitsHandlers.empty());
    renderPage();
    expect(await screen.findByTestId('habit-list-empty')).toBeInTheDocument();
  });
});
