import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { stairTrack, STAIR_COOL_DOWN, STAIR_WARM_UP } from '@/mocks/fixtures/cardioTracks';
import { stairCardioItem } from '@/mocks/fixtures/cycle';
import { PlannedCardioItem } from '@/features/gym/PlannedCardioItem';
import { FinisherParts, finisherText } from './FinisherParts';
import { fromForm, segmentFromForm } from './TrackInfoSheet';

describe('FinisherParts', () => {
  it('lists warm-up, work and cool-down with the total', () => {
    render(<FinisherParts work="5 min continuous @ level 6" warmUp={STAIR_WARM_UP} coolDown={STAIR_COOL_DOWN} totalMinutes={11} />);

    const parts = screen.getByRole('list', { name: 'Finisher parts' });
    expect(parts).toHaveTextContent('Warm-up3 min easy · L3 · Z2');
    expect(parts).toHaveTextContent('Work5 min continuous @ level 6');
    expect(parts).toHaveTextContent('Cool-down3 min easy · L3 · Z1–2');
    expect(screen.getByText('11 min total')).toBeInTheDocument();
  });

  it('is just the work line without segments', () => {
    render(<FinisherParts work="20 min continuous" totalMinutes={20} />);

    expect(screen.queryByRole('list')).not.toBeInTheDocument();
    expect(screen.getByText('20 min continuous')).toBeInTheDocument();
  });

  it('builds the one-line text', () => {
    expect(finisherText('5 min continuous @ level 6', STAIR_WARM_UP, STAIR_COOL_DOWN)).toBe(
      '3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)'
    );
  });
});

describe('PlannedCardioItem', () => {
  it('shows the three parts on the plan card and the line plus total in the compact row', () => {
    const { unmount } = render(<PlannedCardioItem cardio={stairCardioItem} />);
    expect(screen.getByTestId('finisher-parts')).toHaveTextContent('11 min total');
    unmount();

    render(
      <ul>
        <PlannedCardioItem cardio={stairCardioItem} compact />
      </ul>
    );
    expect(screen.getByTestId('planned-cardio')).toHaveTextContent('3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3) · 11 min');
  });
});

describe('track info form', () => {
  const stage = stairTrack.stages[0];
  const base = {
    sessions: '2', min: '5', max: '15', step: '1', intensityStart: '6', intensityStep: '1', intensityMax: '9',
    reset: '10', warmMinutes: '3', warmLevel: '3', coolMinutes: '3', coolLevel: '3',
  };

  it('keeps the segments, zone notes and reset-to', () => {
    const result = fromForm(stage, base);

    expect(result.warmUp).toEqual(STAIR_WARM_UP);
    expect(result.coolDown).toEqual(STAIR_COOL_DOWN);
    expect(result.doubleProgression!.volumeResetTo).toBe(10);
    expect(result.doubleProgression!.volumeMax).toBe(15);
  });

  it('turns a segment off at 0 minutes and changes its level', () => {
    const result = fromForm(stage, { ...base, warmMinutes: '0', coolMinutes: '5', coolLevel: '2' });

    expect(result.warmUp).toBeNull();
    expect(result.coolDown).toEqual({ minutes: 5, level: 2, zone: 'Z1–2' });
  });

  it('drops a reset-to outside the range (back to the bottom)', () => {
    expect(fromForm(stage, { ...base, reset: '20' }).doubleProgression!.volumeResetTo).toBeNull();
    expect(fromForm(stage, { ...base, reset: '' }).doubleProgression!.volumeResetTo).toBeNull();
  });

  it('gives a new segment the default zone note', () => {
    expect(segmentFromForm('4', '', null, 'Z2')).toEqual({ minutes: 4, level: null, zone: 'Z2' });
  });
});
