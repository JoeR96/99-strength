import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { planTemplatesApi } from '@/api/planTemplates';
import { planV3Preview } from '@/mocks/fixtures/planTemplates';
import { installPlanTemplatesHandlers, planTemplatesHandlers } from '@/mocks/handlers/planTemplates';
import { ApplyPlanForm } from './ApplyPlanForm';

function renderForm(onApplied = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ApplyPlanForm template={planV3Preview} onApplied={onApplied} />
    </QueryClientProvider>
  );
  return onApplied;
}

describe('ApplyPlanForm when the template is already the active plan', () => {
  it('offers a fresh copy and resends with confirmReplace', async () => {
    installPlanTemplatesHandlers(planTemplatesHandlers.alreadyRunning());
    const spy = vi.spyOn(planTemplatesApi, 'apply');
    const onApplied = renderForm();

    fireEvent.click(screen.getByRole('button', { name: /^start plan v3/i }));
    expect(await screen.findByTestId('fresh-copy-confirm')).toHaveTextContent(/already your plan/i);
    expect(onApplied).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('fresh-copy-button'));
    await waitFor(() => expect(onApplied).toHaveBeenCalledWith(expect.objectContaining({ alreadyStarted: false })));
    expect(spy).toHaveBeenLastCalledWith('plan-v3', expect.objectContaining({ confirmReplace: true }));
  });

  it('keeps the current copy without resending', async () => {
    installPlanTemplatesHandlers(planTemplatesHandlers.alreadyRunning());
    const spy = vi.spyOn(planTemplatesApi, 'apply');
    const onApplied = renderForm();

    fireEvent.click(screen.getByRole('button', { name: /^start plan v3/i }));
    fireEvent.click(await screen.findByRole('button', { name: 'Keep current' }));

    expect(onApplied).toHaveBeenCalledWith(expect.objectContaining({ alreadyStarted: true }));
    expect(spy).toHaveBeenCalledTimes(1);
  });
});
