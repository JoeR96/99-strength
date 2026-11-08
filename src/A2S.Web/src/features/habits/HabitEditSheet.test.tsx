import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { HabitEditSheet } from './HabitEditSheet';
import { validateHabitForm, EMPTY_HABIT_FORM } from './habitForm';
import { habitDefinitions } from '@/mocks/fixtures/habits';

describe('validateHabitForm', () => {
  it('rejects a minimum above the target', () => {
    const result = validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Water', target: '6', minimum: '7' });
    expect(result.request).toBeUndefined();
    expect(result.errors.minimum).toMatch(/above the target/i);
  });

  it('accepts minimum equal to target and trims the unit', () => {
    const result = validateHabitForm({ ...EMPTY_HABIT_FORM, name: ' Water ', target: '6', minimum: '6', unit: ' bottles ' });
    expect(result.errors).toEqual({});
    expect(result.request).toEqual({ name: 'Water', type: 'Counter', target: 6, minimum: 6, unit: 'bottles', icon: null });
  });

  it('forces 1/1 for a checkbox', () => {
    const result = validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Creatine', type: 'Checkbox', target: '9', minimum: '9' });
    expect(result.request).toMatchObject({ target: 1, minimum: 1 });
  });

  it('requires a name and a positive target', () => {
    const result = validateHabitForm({ ...EMPTY_HABIT_FORM, name: '', target: '0', minimum: '0' });
    expect(result.errors.name).toBeDefined();
    expect(result.errors.target).toBeDefined();
  });
});

describe('HabitEditSheet', () => {
  it('shows an inline error when the minimum is above the target and does not save', () => {
    const onSave = vi.fn();
    render(<HabitEditSheet open habit={habitDefinitions[0]} onClose={vi.fn()} onSave={onSave} />);
    fireEvent.change(screen.getByLabelText(/minimum/i), { target: { value: '9' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(screen.getByTestId('minimum-error')).toHaveTextContent(/above the target/i);
    expect(screen.getByLabelText(/minimum/i)).toHaveAttribute('aria-invalid', 'true');
    expect(onSave).not.toHaveBeenCalled();
  });

  it('saves an edit with the existing type', () => {
    const onSave = vi.fn();
    render(<HabitEditSheet open habit={habitDefinitions[0]} onClose={vi.fn()} onSave={onSave} />);
    expect(screen.queryByRole('button', { name: /^counter/i })).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/target/i), { target: { value: '8' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Water', type: 'Counter', target: 8, minimum: 4 }),
      habitDefinitions[0]
    );
  });

  it('archive asks for confirmation first', () => {
    const onArchive = vi.fn();
    render(<HabitEditSheet open habit={habitDefinitions[0]} onClose={vi.fn()} onSave={vi.fn()} onArchive={onArchive} />);
    fireEvent.click(screen.getByRole('button', { name: /^archive$/i }));
    expect(onArchive).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /confirm archive/i }));
    expect(onArchive).toHaveBeenCalledWith(habitDefinitions[0], true);
  });

  it('creating lets you pick the type and hides target for a tick', () => {
    render(<HabitEditSheet open habit={null} onClose={vi.fn()} onSave={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /tick/i }));
    expect(screen.queryByLabelText(/target/i)).not.toBeInTheDocument();
  });
});
