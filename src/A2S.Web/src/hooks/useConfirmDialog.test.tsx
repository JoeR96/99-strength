import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { useConfirmDialog } from './useConfirmDialog';

function TestComponent() {
  const { confirm, ConfirmDialog } = useConfirmDialog();
  return (
    <div>
      <button
        onClick={async () => {
          const result = await confirm({
            title: 'Delete?',
            description: 'Are you sure?',
            confirmLabel: 'Yes',
            cancelLabel: 'No',
            variant: 'destructive',
          });
          document.getElementById('result')!.textContent = String(result);
        }}
      >
        Open
      </button>
      <span id="result" />
      {ConfirmDialog}
    </div>
  );
}

function SimpleTestComponent() {
  const { confirm, ConfirmDialog } = useConfirmDialog();
  return (
    <div>
      <button
        onClick={async () => {
          const result = await confirm({
            title: 'Confirm',
            description: 'Continue?',
          });
          document.getElementById('result')!.textContent = String(result);
        }}
      >
        Open
      </button>
      <span id="result" />
      {ConfirmDialog}
    </div>
  );
}

describe('useConfirmDialog', () => {
  it('does not render dialog initially', () => {
    render(<TestComponent />);
    expect(screen.queryByText('Delete?')).not.toBeInTheDocument();
  });

  it('opens dialog on confirm call', async () => {
    render(<TestComponent />);
    fireEvent.click(screen.getByText('Open'));
    await waitFor(() => {
      expect(screen.getByText('Delete?')).toBeInTheDocument();
      expect(screen.getByText('Are you sure?')).toBeInTheDocument();
    });
  });

  it('shows custom button labels', async () => {
    render(<TestComponent />);
    fireEvent.click(screen.getByText('Open'));
    await waitFor(() => {
      expect(screen.getByText('Yes')).toBeInTheDocument();
      expect(screen.getByText('No')).toBeInTheDocument();
    });
  });

  it('resolves true on confirm', async () => {
    render(<TestComponent />);
    fireEvent.click(screen.getByText('Open'));
    await waitFor(() => expect(screen.getByText('Yes')).toBeInTheDocument());
    fireEvent.click(screen.getByText('Yes'));
    await waitFor(() => {
      expect(document.getElementById('result')!.textContent).toBe('true');
    });
  });

  it('resolves false on cancel', async () => {
    render(<TestComponent />);
    fireEvent.click(screen.getByText('Open'));
    await waitFor(() => expect(screen.getByText('No')).toBeInTheDocument());
    fireEvent.click(screen.getByText('No'));
    await waitFor(() => {
      expect(document.getElementById('result')!.textContent).toBe('false');
    });
  });

  it('uses default button labels when not provided', async () => {
    render(<SimpleTestComponent />);
    fireEvent.click(screen.getByText('Open'));
    await waitFor(() => {
      expect(screen.getByText('Continue')).toBeInTheDocument();
      expect(screen.getByText('Cancel')).toBeInTheDocument();
    });
  });
});
