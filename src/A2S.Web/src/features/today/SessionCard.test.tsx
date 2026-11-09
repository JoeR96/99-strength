import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { SessionCard, type SessionCardProps } from './SessionCard';
import { HEVY_ROUTINE_ID, todayCardio, todayDone, todayRest, todayUpperA } from '@/mocks/fixtures/plan';
import { SessionStatus } from '@/types/plan';

const DATE = '2026-11-09';
const upperA = todayUpperA(DATE).sessions[0];
const pushedUpperA = { ...upperA, hevyRoutineId: HEVY_ROUTINE_ID };
const fourByFour = todayCardio(DATE).sessions[0];

function renderCard(props: SessionCardProps) {
  return render(
    <MemoryRouter>
      <SessionCard {...props} />
    </MemoryRouter>
  );
}

describe('SessionCard', () => {
  it('without a Hevy key shows Start on its own', () => {
    renderCard({ session: upperA });

    expect(screen.getByTestId('start-session')).toHaveAttribute('href', '/workout/session/1');
    expect(screen.queryByTestId('open-in-hevy')).not.toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
  });

  it('with a pushed routine shows Open in Hevy beside Start and hands the session over', () => {
    const onOpenInHevy = vi.fn();
    renderCard({ session: pushedUpperA, hevyConfigured: true, onOpenInHevy });

    expect(screen.getByTestId('start-session')).toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Open in Hevy: Upper A' }));

    expect(onOpenInHevy).toHaveBeenCalledWith(pushedUpperA);
  });

  it('a routine pushed from another device is still offered when this card has no key flag', () => {
    renderCard({ session: pushedUpperA, hevyConfigured: false });
    expect(screen.getByTestId('open-in-hevy')).toBeInTheDocument();
  });

  it('with a key and no routine shows Push to Hevy', () => {
    const onPushToHevy = vi.fn();
    renderCard({ session: upperA, hevyConfigured: true, onPushToHevy });

    expect(screen.queryByTestId('open-in-hevy')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Push to Hevy: Upper A' }));

    expect(onPushToHevy).toHaveBeenCalledWith(upperA);
  });

  it('while pushing the button says so and cannot be pressed again', () => {
    const onPushToHevy = vi.fn();
    renderCard({ session: upperA, hevyConfigured: true, onPushToHevy, pushing: true });

    const button = screen.getByTestId('push-to-hevy');
    expect(button).toBeDisabled();
    expect(button).toHaveTextContent('Pushing…');
    fireEvent.click(button);
    expect(onPushToHevy).not.toHaveBeenCalled();
  });

  it('cardio gets Mark done with the Hevy button beside it', () => {
    const onMarkDone = vi.fn();
    renderCard({ session: fourByFour, hevyConfigured: true, onMarkDone });

    fireEvent.click(screen.getByTestId('mark-done'));
    expect(onMarkDone).toHaveBeenCalledWith(fourByFour);
    expect(screen.getByTestId('push-to-hevy')).toBeInTheDocument();
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
  });

  it('only a planned session gets a Hevy button: not done, missed, paused or rest', () => {
    const done = { ...todayDone(DATE).sessions[0], hevyRoutineId: HEVY_ROUTINE_ID };
    const { unmount: unmountDone } = renderCard({ session: done, hevyConfigured: true, onUndo: vi.fn() });
    expect(screen.queryByTestId('open-in-hevy')).not.toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
    unmountDone();

    const { unmount: unmountMissed } = renderCard({ session: { ...upperA, status: SessionStatus.Missed }, hevyConfigured: true });
    expect(screen.getByTestId('start-session')).toBeInTheDocument();
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
    unmountMissed();

    const { unmount: unmountPaused } = renderCard({ session: pushedUpperA, hevyConfigured: true, paused: true });
    expect(screen.queryByTestId('start-session')).not.toBeInTheDocument();
    expect(screen.queryByTestId('open-in-hevy')).not.toBeInTheDocument();
    unmountPaused();

    renderCard({ session: todayRest(DATE).sessions[0], hevyConfigured: true });
    expect(screen.queryByTestId('push-to-hevy')).not.toBeInTheDocument();
  });

  it('every button is tall enough to tap (the 56 px size, never the small one)', () => {
    renderCard({ session: pushedUpperA, hevyConfigured: true });

    for (const element of [screen.getByTestId('start-session'), screen.getByTestId('open-in-hevy')]) {
      expect(element.className).toContain('h-14');
      expect(element.className).not.toContain('h-10');
    }
  });
});
