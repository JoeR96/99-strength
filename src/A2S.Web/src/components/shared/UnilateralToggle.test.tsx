import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { UnilateralToggle } from './UnilateralToggle';

describe('UnilateralToggle', () => {
  it('renders with label and description', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} />);
    expect(screen.getByText('Unilateral Exercise')).toBeInTheDocument();
    expect(screen.getByText(/one side at a time/)).toBeInTheDocument();
  });

  it('renders unchecked when isUnilateral is false', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} />);
    const toggle = screen.getByRole('switch');
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('renders checked when isUnilateral is true', () => {
    render(<UnilateralToggle isUnilateral={true} onChange={vi.fn()} />);
    const toggle = screen.getByRole('switch');
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  it('calls onChange when clicked', () => {
    const onChange = vi.fn();
    render(<UnilateralToggle isUnilateral={false} onChange={onChange} />);
    const toggle = screen.getByRole('switch');
    fireEvent.click(toggle);
    expect(onChange).toHaveBeenCalledWith(true);
  });

  it('is disabled when disabled prop is true', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} disabled={true} />);
    const toggle = screen.getByRole('switch');
    expect(toggle).toBeDisabled();
  });
});
