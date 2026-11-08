import { describe, it, expect, vi, beforeEach } from 'vitest';
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
    getMeals: vi.fn(),
    getWeekMeals: vi.fn(),
    setWeekMeals: vi.fn(),
    generateShoppingList: vi.fn(),
  },
}));

import { mealsApi } from '@/api/meals';
import { MealsPage } from './MealsPage';
import { WEEK, barbacoa, emptyWeek, generatedList, meals, oats, orzo, weekWithOrzoAndBarbacoa } from './mealsFixtures';

function renderPage(week = WEEK) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/meals?week=${week}`]}>
        <Routes>
          <Route path="/meals" element={<MealsPage />} />
          <Route path="/shopping-list" element={<div data-testid="shopping-list-page">Shopping list page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('MealsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(mealsApi.getMeals).mockResolvedValue(meals);
    vi.mocked(mealsApi.getWeekMeals).mockResolvedValue(weekWithOrzoAndBarbacoa);
    vi.mocked(mealsApi.setWeekMeals).mockImplementation(async (weekStart, mealIds) => ({ weekStart, mealIds }));
    vi.mocked(mealsApi.generateShoppingList).mockResolvedValue(generatedList);
  });

  it('lists meals grouped by type with the week in the header', async () => {
    renderPage();

    expect(await screen.findByText(orzo.title)).toBeInTheDocument();
    expect(screen.getByText(oats.title)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Breakfast' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dinner' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Snack' })).not.toBeInTheDocument();
    expect(screen.getByText('Mon 9 Nov – Sun 15 Nov')).toBeInTheDocument();
    expect(mealsApi.getWeekMeals).toHaveBeenCalledWith(WEEK);
  });

  it('shows which meals are already on this week and the count', async () => {
    renderPage();

    const orzoBox = await screen.findByRole('checkbox', { name: `Add ${orzo.title} to this week` });
    await waitFor(() => expect(orzoBox).toBeChecked());
    expect(screen.getByRole('checkbox', { name: `Add ${barbacoa.title} to this week` })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: `Add ${oats.title} to this week` })).not.toBeChecked();
    expect(screen.getByText('2 meals this week')).toBeInTheDocument();
  });

  it('ticking a meal saves the week menu', async () => {
    renderPage();
    const oatsBox = await screen.findByRole('checkbox', { name: `Add ${oats.title} to this week` });
    await waitFor(() => expect(screen.getByText('2 meals this week')).toBeInTheDocument());

    fireEvent.click(oatsBox);

    await waitFor(() => expect(mealsApi.setWeekMeals).toHaveBeenCalledWith(WEEK, [orzo.id, barbacoa.id, oats.id]));
    expect(await screen.findByText('3 meals this week')).toBeInTheDocument();
  });

  it('unticking a meal removes it from the week', async () => {
    renderPage();
    const orzoBox = await screen.findByRole('checkbox', { name: `Add ${orzo.title} to this week` });
    await waitFor(() => expect(orzoBox).toBeChecked());

    fireEvent.click(orzoBox);

    await waitFor(() => expect(mealsApi.setWeekMeals).toHaveBeenCalledWith(WEEK, [barbacoa.id]));
  });

  it('generate is disabled with nothing picked', async () => {
    vi.mocked(mealsApi.getWeekMeals).mockResolvedValue(emptyWeek);
    renderPage();

    await screen.findByText(orzo.title);
    await waitFor(() => expect(screen.getByText('No meals picked yet')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Generate list' })).toBeDisabled();
  });

  it('generate builds the list for the week and opens it', async () => {
    renderPage();
    await screen.findByText(orzo.title);
    const button = screen.getByRole('button', { name: 'Generate list' });
    await waitFor(() => expect(button).toBeEnabled());

    fireEvent.click(button);

    await waitFor(() => expect(mealsApi.generateShoppingList).toHaveBeenCalledWith(WEEK));
    expect(await screen.findByTestId('shopping-list-page')).toBeInTheDocument();
  });

  it('snaps a mid-week date to its Monday', async () => {
    renderPage('2026-11-12');

    await screen.findByText(orzo.title);
    expect(mealsApi.getWeekMeals).toHaveBeenCalledWith(WEEK);
  });

  it('shows an error with retry when meals fail to load', async () => {
    vi.mocked(mealsApi.getMeals).mockRejectedValue(new Error('boom'));
    renderPage();

    expect(await screen.findByText('Failed to load meals')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
