import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { PlanNameWithRename, planNameError } from './RenamePlanDialog';
import { plansApi } from '@/api/plans';
import { installHandlers, rejectWith } from '@/mocks/handlers/install';
import { planFixture } from '@/mocks/fixtures/plan';

vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

function renderName() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <PlanNameWithRename planId="plan-1" name="Cut to 85 — Nov 2026" />
    </QueryClientProvider>
  );
}

describe('planNameError', () => {
  it('needs a non-blank name of at most 200 characters', () => {
    expect(planNameError('   ')).toMatch(/name/);
    expect(planNameError('a'.repeat(201))).toMatch(/200/);
    expect(planNameError(` ${'a'.repeat(200)} `)).toBeNull();
  });
});

describe('PlanNameWithRename', () => {
  beforeEach(() => vi.mocked(toast.success).mockClear());

  it('opens the dialog from the pencil, saves the trimmed name and shows a toast', async () => {
    const rename = vi.fn(async (_id: string, name: string) => ({ ...planFixture, name }));
    installHandlers(plansApi, { rename });
    renderName();

    await userEvent.click(screen.getByRole('button', { name: 'Rename plan' }));
    const field = screen.getByLabelText('Plan name');
    expect(field).toHaveValue('Cut to 85 — Nov 2026');
    await userEvent.clear(field);
    await userEvent.type(field, '  Lumbridge Lard Shed  ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(rename).toHaveBeenCalledWith('plan-1', 'Lumbridge Lard Shed'));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Plan renamed to Lumbridge Lard Shed'));
    await waitFor(() => expect(screen.queryByLabelText('Plan name')).not.toBeInTheDocument());
  });

  it('refuses a blank name without calling the server', async () => {
    const rename = vi.fn();
    installHandlers(plansApi, { rename });
    renderName();

    await userEvent.click(screen.getByRole('button', { name: 'Rename plan' }));
    await userEvent.clear(screen.getByLabelText('Plan name'));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Give the plan a name.');
    expect(rename).not.toHaveBeenCalled();
  });

  it('shows the server error and stays open', async () => {
    installHandlers(plansApi, { rename: rejectWith(404, 'Training plan not found.') });
    renderName();

    await userEvent.click(screen.getByRole('button', { name: 'Rename plan' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Training plan not found.');
    expect(screen.getByLabelText('Plan name')).toBeInTheDocument();
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('Cancel closes without saving', async () => {
    const rename = vi.fn();
    installHandlers(plansApi, { rename });
    renderName();

    await userEvent.click(screen.getByRole('button', { name: 'Rename plan' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByLabelText('Plan name')).not.toBeInTheDocument());
    expect(rename).not.toHaveBeenCalled();
  });
});
