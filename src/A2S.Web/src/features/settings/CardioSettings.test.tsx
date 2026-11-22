import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { HeartRateSettings, validateZones } from './HeartRateSettings';
import { RoutinePushSetting } from './RoutinePushSetting';
import { cardioTracksApi } from '@/api/cardioTracks';
import { usersApi } from '@/api/users';

vi.mock('react-hot-toast', () => ({ default: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

const zones = {
  maxHeartRate: 190,
  lowerPercents: [50, 60, 70, 80, 90],
  bands: [],
};

function wrap(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('HeartRateSettings', () => {
  beforeEach(() => {
    vi.spyOn(cardioTracksApi, 'getZones').mockResolvedValue(zones);
  });

  it('shows the bpm range of each zone and saves edited floors', async () => {
    const setZones = vi.spyOn(cardioTracksApi, 'setZones').mockResolvedValue({ ...zones, lowerPercents: [55, 65, 75, 85, 92] });
    wrap(<HeartRateSettings />);

    expect(await screen.findByText('114–133')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Z1'), { target: { value: '55' } });
    fireEvent.change(screen.getByLabelText('Z2'), { target: { value: '65' } });
    fireEvent.change(screen.getByLabelText('Z3'), { target: { value: '75' } });
    fireEvent.change(screen.getByLabelText('Z4'), { target: { value: '85' } });
    fireEvent.change(screen.getByLabelText('Z5'), { target: { value: '92' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(setZones).toHaveBeenCalledWith(190, [55, 65, 75, 85, 92]));
  });

  it('sends null zones for the WHOOP defaults, and refuses floors that do not rise', async () => {
    const setZones = vi.spyOn(cardioTracksApi, 'setZones').mockResolvedValue(zones);
    wrap(<HeartRateSettings />);

    fireEvent.change(await screen.findByLabelText('Z3'), { target: { value: '55' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByRole('alert')).toHaveTextContent('start above');

    fireEvent.click(screen.getByRole('button', { name: 'WHOOP zones' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(setZones).toHaveBeenCalledWith(190, null));
  });

  it('validates max HR', () => {
    expect(validateZones(250, [50, 60, 70, 80, 90])).toMatch(/100 to 240/);
    expect(validateZones(null, [50, 60, 70, 80, 90])).toBeNull();
  });
});

describe('RoutinePushSetting', () => {
  it('is on by default and turns off', async () => {
    vi.spyOn(usersApi, 'getPreferences').mockResolvedValue({ pushRoutinesToHevy: true });
    const set = vi.spyOn(usersApi, 'setPreferences').mockResolvedValue({ pushRoutinesToHevy: false });
    wrap(<RoutinePushSetting />);

    const toggle = await screen.findByRole('switch', { name: 'Push routines into Hevy' });
    expect(toggle).toBeChecked();
    fireEvent.click(toggle);

    await waitFor(() => expect(set).toHaveBeenCalledWith({ pushRoutinesToHevy: false }));
  });
});
