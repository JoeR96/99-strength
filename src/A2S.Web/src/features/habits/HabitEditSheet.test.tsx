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
    expect(result.request).toEqual({ name: 'Water', type: 'Counter', target: 6, minimum: 6, unit: 'bottles', icon: null, step: 1 });
  });

  it('forces 1/1 for a checkbox', () => {
    const result = validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Creatine', type: 'Checkbox', target: '9', minimum: '9' });
    expect(result.request).toMatchObject({ target: 1, minimum: 1 });
  });

  it('keeps a counter step and rejects a step of 0; a tick has no step', () => {
    expect(validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Protein', target: '180', minimum: '140', step: '25' }).request?.step).toBe(25);
    expect(validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Protein', step: '0' }).errors.step).toMatch(/more than 0/i);
    expect(validateHabitForm({ ...EMPTY_HABIT_FORM, name: 'Creatine', type: 'Checkbox', step: '0' }).request).not.toHaveProperty('step');
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

  it('creates a counter with a step and the meat icon', () => {
    const onSave = vi.fn();
    render(<HabitEditSheet open habit={null} onClose={vi.fn()} onSave={onSave} />);
    fireEvent.change(screen.getByLabelText(/name/i), { target: { value: 'Protein' } });
    fireEvent.change(screen.getByLabelText(/target/i), { target: { value: '180' } });
    fireEvent.change(screen.getByLabelText(/minimum/i), { target: { value: '140' } });
    fireEvent.change(screen.getByLabelText(/^step$/i), { target: { value: '25' } });
    fireEvent.click(screen.getByRole('button', { name: 'meat' }));
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ name: 'Protein', step: 25, icon: 'meat' }), null);
  });

  it('hides the step for a tick habit', () => {
    render(<HabitEditSheet open habit={null} onClose={vi.fn()} onSave={vi.fn()} />);
    expect(screen.getByLabelText(/^step$/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^tick/i }));
    expect(screen.queryByLabelText(/^step$/i)).not.toBeInTheDocument();
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
