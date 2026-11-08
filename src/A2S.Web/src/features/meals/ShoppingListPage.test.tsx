import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { type ReactNode } from 'react';

vi.mock('@clerk/clerk-react', () => ({
  useUser: () => ({ user: { firstName: 'Joe' } }),
  useAuth: () => ({ isLoaded: true, isSignedIn: true, getToken: vi.fn() }),
  ClerkProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock('@/components/layout/Navbar', () => ({
  Navbar: () => <nav data-testid="navbar">Navbar</nav>,
}));

vi.mock('@/api/meals', () => ({
  mealsApi: {
    getShoppingList: vi.fn(),
    toggleShoppingItem: vi.fn(),
    generateShoppingList: vi.fn(),
    exportShoppingList: vi.fn(),
  },
}));

import { mealsApi } from '@/api/meals';
import { ShoppingListPage } from './ShoppingListPage';
import { WEEK, emptyList, generatedList, generatedListText, orzo } from './mealsFixtures';

function renderPage(week = WEEK) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/shopping-list?week=${week}`]}>
        <Routes>
          <Route path="/shopping-list" element={<ShoppingListPage />} />
          <Route path="/meals" element={<div data-testid="meals-page">Meals page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('ShoppingListPage', () => {
  const writeText = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    vi.mocked(mealsApi.getShoppingList).mockResolvedValue(generatedList);
    vi.mocked(mealsApi.toggleShoppingItem).mockImplementation(async (_listId, name, unit) => ({
      ...generatedList,
      items: generatedList.items.map((i) => (i.name === name && i.unit === unit ? { ...i, checked: !i.checked } : i)),
      checkedCount: 2,
    }));
    vi.mocked(mealsApi.exportShoppingList).mockResolvedValue(generatedListText);
    vi.mocked(mealsApi.generateShoppingList).mockResolvedValue(generatedList);
  });

  afterEach(() => {
    Reflect.deleteProperty(navigator, 'clipboard');
  });

  it('renders summed lines, progress and which lines come from several meals', async () => {
    renderPage();

    expect(await screen.findByText('6 clove garlic')).toBeInTheDocument();
    expect(screen.getByText('1.2 kg beef chuck')).toBeInTheDocument();
    expect(screen.getByText('salt')).toBeInTheDocument();
    expect(screen.getByText('1 of 5 ticked')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '1');
    expect(screen.getByText('2 meals')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /6 clove garlic/ })).toHaveAttribute('aria-pressed', 'true');
  });

  it('tapping a row toggles it on the server and updates the progress', async () => {
    renderPage();
    const row = await screen.findByRole('button', { name: /200 g orzo/ });

    fireEvent.click(row);

    await waitFor(() => expect(mealsApi.toggleShoppingItem).toHaveBeenCalledWith(generatedList.id, 'orzo', 'g'));
    expect(await screen.findByText('2 of 5 ticked')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /200 g orzo/ })).toHaveAttribute('aria-pressed', 'true');
  });

  it('copy fetches the text export and writes it to the clipboard', async () => {
    renderPage();
    await screen.findByText('6 clove garlic');

    fireEvent.click(screen.getByRole('button', { name: 'Copy' }));

    await waitFor(() => expect(mealsApi.exportShoppingList).toHaveBeenCalledWith(WEEK, 'text'));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(generatedListText));
  });

  it('share falls back to the clipboard when the browser cannot share', async () => {
    renderPage();
    await screen.findByText('6 clove garlic');

    fireEvent.click(screen.getByRole('button', { name: 'Share' }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(generatedListText));
  });

  it('csv fetches the csv export and downloads it', async () => {
    const createObjectURL = vi.fn().mockReturnValue('blob:list');
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    vi.mocked(mealsApi.exportShoppingList).mockResolvedValue('name,quantity,unit,checked\n');
    renderPage();
    await screen.findByText('6 clove garlic');

    fireEvent.click(screen.getByRole('button', { name: 'CSV' }));

    await waitFor(() => expect(mealsApi.exportShoppingList).toHaveBeenCalledWith(WEEK, 'csv'));
    await waitFor(() => expect(click).toHaveBeenCalled());
    expect(createObjectURL).toHaveBeenCalled();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:list');
    click.mockRestore();
  });

  it('with no list yet, points at the meals page', async () => {
    vi.mocked(mealsApi.getShoppingList).mockResolvedValue(emptyList);
    renderPage();

    expect(await screen.findByText('Nothing on the list yet')).toBeInTheDocument();
    expect(screen.getByText('Pick some meals for this week first.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pick meals' })).toHaveAttribute('href', `/meals?week=${WEEK}`);
    expect(screen.queryByRole('button', { name: 'Generate list' })).not.toBeInTheDocument();
  });

  it('with meals picked but no items, offers to generate', async () => {
    vi.mocked(mealsApi.getShoppingList).mockResolvedValue({ ...emptyList, id: generatedList.id, sourceMealIds: [orzo.id] });
    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: 'Generate list' }));

    await waitFor(() => expect(mealsApi.generateShoppingList).toHaveBeenCalledWith(WEEK));
    expect(await screen.findByText('6 clove garlic')).toBeInTheDocument();
  });

  it('snaps a mid-week date to its Monday', async () => {
    renderPage('2026-11-15');

    await screen.findByText('6 clove garlic');
    expect(mealsApi.getShoppingList).toHaveBeenCalledWith(WEEK);
  });
});
