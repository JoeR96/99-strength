import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { GymPreview } from './GymPreview';
import { cardioGymSession, stairStage1Block, stairStage2PendingBlock, walkPendingBlock } from './gymFixtures';

const renderPreview = (state = cardioGymSession()) =>
  render(
    <MemoryRouter>
      <GymPreview state={state} dayLabel="Tomorrow · Tue 24 Nov" startHref={null} />
    </MemoryRouter>,
  );

describe('GymPreview cardio exercises', () => {
  it('lists each cardio exercise with its block text after the lifts', () => {
    renderPreview();

    const stairs = screen.getByTestId('preview-cardio-v3-stairs');
    expect(stairs).toHaveTextContent('Stair Machine (Floors)');
    expect(stairs).toHaveTextContent(stairStage1Block.text);
    expect(screen.getByTestId('preview-cardio-v3-walk')).toHaveTextContent('10 min @ 5 % incline · 5.5 km/h');
    const lift = screen.getByTestId('preview-lift-0');
    expect(lift.compareDocumentPosition(stairs) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('says what to set first while the settings are pending', () => {
    renderPreview(cardioGymSession(stairStage2PendingBlock, walkPendingBlock));

    expect(screen.getByTestId('preview-cardio-v3-stairs')).toHaveTextContent('Set the hard level first');
    expect(screen.getByTestId('preview-cardio-v3-walk')).toHaveTextContent('Set speed and incline first');
  });
});
