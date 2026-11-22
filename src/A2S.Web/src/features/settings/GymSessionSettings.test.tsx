import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { GymSessionSettings } from './GymSessionSettings';
import { planFixture } from '@/mocks/fixtures/plan';
import { mobilityA, stairFinisher } from '@/features/gym/gymFixtures';
import { plansApi } from '@/api/plans';
import { mobilityRoutinesApi } from '@/api/mobilityRoutines';
import { cardioProtocolsApi } from '@/api/gymSessions';

vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

function renderSettings() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <GymSessionSettings />
    </QueryClientProvider>
  );
}

describe('GymSessionSettings', () => {
  beforeEach(() => {
    vi.spyOn(plansApi, 'getActive').mockResolvedValue(planFixture);
    vi.spyOn(mobilityRoutinesApi, 'list').mockResolvedValue([mobilityA]);
    vi.spyOn(cardioProtocolsApi, 'list').mockResolvedValue([stairFinisher]);
  });

  it('sets a lift slot’s finisher and saves the whole schedule', async () => {
    const update = vi.spyOn(plansApi, 'updateSchedule').mockResolvedValue({ plan: planFixture });
    renderSettings();

    const finisher = (await screen.findAllByLabelText('Finisher'))[0];
    await screen.findAllByRole('option', { name: 'Stair finisher' });
    fireEvent.change(finisher, { target: { value: stairFinisher.id } });
    fireEvent.click(screen.getByRole('button', { name: 'Save gym sessions' }));

    await waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    const [planId, schedule] = update.mock.calls[0];
    expect(planId).toBe(planFixture.id);
    expect(schedule).toHaveLength(planFixture.schedule.length);
    expect(schedule.filter((s) => s.finisherCardioProtocolId === stairFinisher.id)).toHaveLength(1);
  });
});
