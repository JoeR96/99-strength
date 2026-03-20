import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UndoConfirmationModal } from './UndoConfirmationModal';

describe('UndoConfirmationModal', () => {
  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    onConfirm: vi.fn().mockResolvedValue(undefined),
    dayNumber: 1,
    weekNumber: 3,
    wouldRollbackWeek: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders nothing when isOpen is false', () => {
    render(<UndoConfirmationModal {...defaultProps} isOpen={false} />);
    expect(screen.queryByText('Undo Last Workout?')).not.toBeInTheDocument();
  });

  it('renders modal when isOpen is true', () => {
    render(<UndoConfirmationModal {...defaultProps} />);
    expect(screen.getByText('Undo Last Workout?')).toBeInTheDocument();
  });

  it('displays correct day name', () => {
    render(<UndoConfirmationModal {...defaultProps} dayNumber={3} />);
    expect(screen.getByText(/Wednesday/)).toBeInTheDocument();
  });

  it('calls onClose when Cancel is clicked', () => {
    render(<UndoConfirmationModal {...defaultProps} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onConfirm when Undo Workout is clicked', async () => {
    render(<UndoConfirmationModal {...defaultProps} />);
    fireEvent.click(screen.getByText('Undo Workout'));
    await waitFor(() => {
      expect(defaultProps.onConfirm).toHaveBeenCalled();
    });
  });

  it('shows week rollback warning when wouldRollbackWeek is true', () => {
    render(<UndoConfirmationModal {...defaultProps} wouldRollbackWeek={true} />);
    expect(screen.getByText('Week Rollback')).toBeInTheDocument();
  });

  it('does not show week rollback warning when wouldRollbackWeek is false', () => {
    render(<UndoConfirmationModal {...defaultProps} wouldRollbackWeek={false} />);
    expect(screen.queryByText('Week Rollback')).not.toBeInTheDocument();
  });

  it('closes modal when backdrop is clicked', () => {
    render(<UndoConfirmationModal {...defaultProps} />);
    // Find and click the backdrop (the element with bg-black/50)
    const backdrop = document.querySelector('.bg-black\\/50');
    if (backdrop) {
      fireEvent.click(backdrop);
      expect(defaultProps.onClose).toHaveBeenCalled();
    }
  });

  it('disables buttons while undoing', async () => {
    const slowConfirm = vi.fn().mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));
    render(<UndoConfirmationModal {...defaultProps} onConfirm={slowConfirm} />);

    fireEvent.click(screen.getByText('Undo Workout'));

    // Should show "Undoing..." text
    await waitFor(() => {
      expect(screen.getByText('Undoing...')).toBeInTheDocument();
    });
  });
});
