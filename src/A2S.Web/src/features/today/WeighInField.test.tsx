import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { WeighInField } from './WeighInField';
import { parseKg } from './weighIn';

describe('WeighInField', () => {
  it('shows an empty decimal input when nothing is logged', () => {
    render(<WeighInField valueKg={null} onSave={vi.fn()} />);
    const input = screen.getByLabelText(/morning weigh-in/i);
    expect(input).toHaveAttribute('inputmode', 'decimal');
    expect(input).toHaveValue('');
    expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
  });

  it('shows the logged value with Edit, not an empty input', () => {
    render(<WeighInField valueKg={94.6} onSave={vi.fn()} />);
    expect(screen.getByText('94.6')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument();
  });

  it('saving the same value again is a no-op', () => {
    const onSave = vi.fn();
    render(<WeighInField valueKg={94.6} onSave={onSave} />);
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    const input = screen.getByLabelText(/morning weigh-in/i);
    expect(input).toHaveValue('94.6');
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument();
  });

  it('saves a changed value', () => {
    const onSave = vi.fn();
    render(<WeighInField valueKg={94.6} onSave={onSave} />);
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText(/morning weigh-in/i), { target: { value: '94,2' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(onSave).toHaveBeenCalledWith(94.2);
  });

  it('rejects nonsense with an inline error', () => {
    const onSave = vi.fn();
    render(<WeighInField valueKg={null} onSave={onSave} />);
    fireEvent.change(screen.getByLabelText(/morning weigh-in/i), { target: { value: 'abc' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText(/enter a weight in kg/i)).toBeInTheDocument();
  });

  it('parseKg accepts commas and one decimal, rejects out-of-range', () => {
    expect(parseKg('94.6')).toBe(94.6);
    expect(parseKg('94,6')).toBe(94.6);
    expect(parseKg('94.65')).toBe(94.7);
    expect(parseKg('5')).toBeNull();
    expect(parseKg('')).toBeNull();
  });
});
