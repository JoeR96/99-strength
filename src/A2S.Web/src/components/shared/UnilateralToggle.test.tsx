import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { UnilateralToggle } from './UnilateralToggle';

describe('UnilateralToggle', () => {
  it('renders with label and bilateral/unilateral buttons', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} />);
    expect(screen.getByText('Exercise Type')).toBeInTheDocument();
    expect(screen.getByText('Bilateral')).toBeInTheDocument();
    expect(screen.getByText('Unilateral')).toBeInTheDocument();
  });

  it('shows bilateral description when isUnilateral is false', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} />);
    expect(screen.getByText(/both sides together/)).toBeInTheDocument();
  });

  it('shows unilateral description when isUnilateral is true', () => {
    render(<UnilateralToggle isUnilateral={true} onChange={vi.fn()} />);
    expect(screen.getByText(/one side at a time/)).toBeInTheDocument();
  });

  it('calls onChange with true when Unilateral clicked', () => {
    const onChange = vi.fn();
    render(<UnilateralToggle isUnilateral={false} onChange={onChange} />);
    fireEvent.click(screen.getByText('Unilateral'));
    expect(onChange).toHaveBeenCalledWith(true);
  });

  it('calls onChange with false when Bilateral clicked', () => {
    const onChange = vi.fn();
    render(<UnilateralToggle isUnilateral={true} onChange={onChange} />);
    fireEvent.click(screen.getByText('Bilateral'));
    expect(onChange).toHaveBeenCalledWith(false);
  });

  it('disables both buttons when disabled prop is true', () => {
    render(<UnilateralToggle isUnilateral={false} onChange={vi.fn()} disabled={true} />);
    expect(screen.getByText('Bilateral')).toBeDisabled();
    expect(screen.getByText('Unilateral')).toBeDisabled();
  });
});
