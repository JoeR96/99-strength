import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { StartSuggestedPlanCard } from './StartSuggestedPlanCard';
import { comingMonday, formatStartDay, suggestedPlansApi, type StartSuggestedPlanResult } from '@/api/suggestedPlans';

vi.mock('@/api/suggestedPlans', async () => {
  const actual = await vi.importActual<typeof import('@/api/suggestedPlans')>('@/api/suggestedPlans');
  return {
    ...actual,
    suggestedPlansApi: { list: vi.fn(), startCutTo85: vi.fn() },
  };
});

const wednesday11Nov2026 = new Date(2026, 10, 11);

const started: StartSuggestedPlanResult = {
  planId: 'plan-1',
  workoutId: 'workout-1',
  startDate: '2026-11-16',
  alreadyStarted: false,
  warnings: [],
};

function renderWithQuery(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function fillTrainingMaxes() {
  fireEvent.change(screen.getByLabelText('Bench press TM'), { target: { value: '100' } });
  fireEvent.change(screen.getByLabelText('Squat TM'), { target: { value: '140' } });
  fireEvent.change(screen.getByLabelText('Overhead press TM'), { target: { value: '60' } });
  fireEvent.change(screen.getByLabelText('Deadlift TM'), { target: { value: '180' } });
}

describe('comingMonday', () => {
  it('snaps a Wednesday to the coming Monday', () => {
    expect(comingMonday(new Date(2026, 10, 11))).toEqual(new Date(2026, 10, 16));
  });

  it('keeps a Monday', () => {
    expect(comingMonday(new Date(2026, 10, 16))).toEqual(new Date(2026, 10, 16));
  });

  it('moves a Sunday to tomorrow', () => {
    expect(comingMonday(new Date(2026, 10, 15))).toEqual(new Date(2026, 10, 16));
  });

  it('formats as weekday day month', () => {
    expect(formatStartDay(new Date(2026, 10, 16))).toBe('Monday 16 Nov');
  });
});

describe('StartSuggestedPlanCard', () => {
  beforeEach(() => {
    vi.mocked(suggestedPlansApi.startCutTo85).mockReset();
  });

  it('shows the plan name, the four TM inputs, a 95 kg start weight and the Monday start', () => {
    renderWithQuery(<StartSuggestedPlanCard today={wednesday11Nov2026} />);

    expect(screen.getByText('Start this plan')).toBeInTheDocument();
    expect(screen.getByText(/Cut to 85, Nov 2026/)).toBeInTheDocument();
    expect(screen.getByLabelText('Bench press TM')).toHaveAttribute('inputmode', 'decimal');
    expect(screen.getByLabelText('Squat TM')).toBeInTheDocument();
    expect(screen.getByLabelText('Overhead press TM')).toBeInTheDocument();
    expect(screen.getByLabelText('Deadlift TM')).toBeInTheDocument();
    expect(screen.getByLabelText('Start weight')).toHaveValue('95');
    expect(screen.getByRole('button', { name: /Start this plan — starts Monday 16 Nov/ })).toBeInTheDocument();
  });

  it('refuses to submit empty or out-of-range training maxes', () => {
    renderWithQuery(<StartSuggestedPlanCard today={wednesday11Nov2026} />);
    fireEvent.change(screen.getByLabelText('Squat TM'), { target: { value: '500' } });

    fireEvent.click(screen.getByRole('button', { name: /Start this plan/ }));

    expect(suggestedPlansApi.startCutTo85).not.toHaveBeenCalled();
    expect(screen.getAllByText('Enter a weight in kg')).toHaveLength(3);
    expect(screen.getByText('Between 20 and 400 kg')).toBeInTheDocument();
    expect(screen.getByLabelText('Squat TM')).toHaveAttribute('aria-invalid', 'true');
  });

  it('posts the five inputs and reports the result', async () => {
    vi.mocked(suggestedPlansApi.startCutTo85).mockResolvedValue(started);
    const onStarted = vi.fn();
    renderWithQuery(<StartSuggestedPlanCard today={wednesday11Nov2026} onStarted={onStarted} />);
    fillTrainingMaxes();
    fireEvent.change(screen.getByLabelText('Start weight'), { target: { value: '96,5' } });

    fireEvent.click(screen.getByRole('button', { name: /Start this plan/ }));

    await waitFor(() => expect(onStarted).toHaveBeenCalledWith(started));
    // TanStack passes the mutation context as a second argument.
    expect(suggestedPlansApi.startCutTo85).toHaveBeenCalledWith(
      { benchTm: 100, squatTm: 140, ohpTm: 60, deadliftTm: 180, startWeightKg: 96.5 },
      expect.anything()
    );
  });

  it('shows the server detail when the start is refused', async () => {
    vi.mocked(suggestedPlansApi.startCutTo85).mockRejectedValue({
      response: { data: { title: 'Business Rule Violation', detail: 'Exercise template missing.' } },
    });
    renderWithQuery(<StartSuggestedPlanCard today={wednesday11Nov2026} />);
    fillTrainingMaxes();

    fireEvent.click(screen.getByRole('button', { name: /Start this plan/ }));

    expect(await screen.findByText('Exercise template missing.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Start this plan/ })).not.toBeDisabled();
  });

  it('prefills the fields it is given', () => {
    renderWithQuery(
      <StartSuggestedPlanCard today={wednesday11Nov2026} initialValues={{ benchTm: 100, startWeightKg: 97 }} />
    );

    expect(screen.getByLabelText('Bench press TM')).toHaveValue('100');
    expect(screen.getByLabelText('Start weight')).toHaveValue('97');
  });
});
