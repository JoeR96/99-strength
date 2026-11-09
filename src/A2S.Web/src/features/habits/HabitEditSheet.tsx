/**
 * Create or edit a habit: name, type (fixed after creation), target, minimum,
 * unit and icon. Editing an existing habit also offers Archive, which hides it
 * from Today and the streak while keeping its history.
 */

import { useState, type FormEvent } from 'react';
import { Archive, ArchiveRestore } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import type { CreateHabitRequest, HabitDto, HabitType } from '@/types/habits';
import { HABIT_ICON_KEYS } from './habitIcons';
import { HabitIcon } from './HabitIcon';
import { EMPTY_HABIT_FORM, formFromHabit, validateHabitForm, type HabitFormErrors, type HabitFormValues } from './habitForm';

export interface HabitEditSheetProps {
  open: boolean;
  /** `null` creates a new habit. */
  habit: HabitDto | null;
  onClose: () => void;
  onSave: (request: Omit<CreateHabitRequest, 'order'>, habit: HabitDto | null) => void;
  onArchive?: (habit: HabitDto, archive: boolean) => void;
  saving?: boolean;
}

const TYPES: { value: HabitType; label: string; hint: string }[] = [
  { value: 'Counter', label: 'Counter', hint: 'Tap +1 towards a target' },
  { value: 'Numeric', label: 'Number', hint: 'Enter a value' },
  { value: 'Checkbox', label: 'Tick', hint: 'Done or not' },
];

export function HabitEditSheet({ open, habit, onClose, onSave, onArchive, saving = false }: HabitEditSheetProps) {
  const [values, setValues] = useState<HabitFormValues>(() => (habit ? formFromHabit(habit) : EMPTY_HABIT_FORM));
  const [errors, setErrors] = useState<HabitFormErrors>({});
  const [confirmArchive, setConfirmArchive] = useState(false);

  const update = <K extends keyof HabitFormValues>(key: K, value: HabitFormValues[K]) => {
    setValues((prev) => {
      const next = { ...prev, [key]: value };
      if (key === 'type' && value === 'Checkbox') {
        next.target = '1';
        next.minimum = '1';
      }
      return next;
    });
    setErrors({});
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const result = validateHabitForm(values);
    setErrors(result.errors);
    if (result.request) onSave(result.request, habit);
  };

  const isCheckbox = values.type === 'Checkbox';

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className="bottom-0 top-auto max-h-[92dvh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        <form onSubmit={submit} className="space-y-4" noValidate>
          <DialogHeader>
            <DialogTitle>{habit ? 'Edit habit' : 'New habit'}</DialogTitle>
            <DialogDescription>
              {habit ? 'Change the target or minimum; the type stays.' : 'A target for good days and a minimum that keeps the streak alive on bad ones.'}
            </DialogDescription>
          </DialogHeader>

          <div>
            <Label htmlFor="habit-name">Name</Label>
            <Input
              id="habit-name"
              value={values.name}
              onChange={(e) => update('name', e.target.value)}
              placeholder="Water"
              className="mt-1 h-12"
              aria-invalid={errors.name ? true : undefined}
              aria-describedby={errors.name ? 'habit-name-error' : undefined}
              autoFocus={!habit}
            />
            {errors.name && (
              <p id="habit-name-error" className="mt-1 text-xs text-destructive">
                {errors.name}
              </p>
            )}
          </div>

          {!habit && (
            <fieldset>
              <legend className="text-sm font-medium">Type</legend>
              <div className="mt-1 grid grid-cols-3 gap-2">
                {TYPES.map((t) => (
                  <button
                    key={t.value}
                    type="button"
                    onClick={() => update('type', t.value)}
                    aria-pressed={values.type === t.value}
                    className={cn(
                      'flex min-h-14 flex-col items-center justify-center rounded-md border px-2 py-2 text-sm',
                      values.type === t.value ? 'border-primary bg-primary/10 text-foreground' : 'border-border text-muted-foreground'
                    )}
                  >
                    <span className="font-medium">{t.label}</span>
                    <span className="text-[11px]">{t.hint}</span>
                  </button>
                ))}
              </div>
            </fieldset>
          )}

          {!isCheckbox && (
            <div className="grid grid-cols-3 gap-3">
              <div>
                <Label htmlFor="habit-target">Target</Label>
                <Input
                  id="habit-target"
                  inputMode="decimal"
                  value={values.target}
                  onChange={(e) => update('target', e.target.value)}
                  className="mt-1 h-12"
                  aria-invalid={errors.target ? true : undefined}
                  aria-describedby={errors.target ? 'habit-target-error' : undefined}
                />
              </div>
              <div>
                <Label htmlFor="habit-minimum">Minimum</Label>
                <Input
                  id="habit-minimum"
                  inputMode="decimal"
                  value={values.minimum}
                  onChange={(e) => update('minimum', e.target.value)}
                  className="mt-1 h-12"
                  aria-invalid={errors.minimum ? true : undefined}
                  aria-describedby={errors.minimum ? 'habit-minimum-error' : undefined}
                />
              </div>
              <div>
                <Label htmlFor="habit-unit">Unit</Label>
                <Input
                  id="habit-unit"
                  value={values.unit}
                  onChange={(e) => update('unit', e.target.value)}
                  placeholder="bottles"
                  className="mt-1 h-12"
                />
              </div>
              {(errors.target || errors.minimum) && (
                <div className="col-span-3 space-y-1">
                  {errors.target && (
                    <p id="habit-target-error" className="text-xs text-destructive">
                      {errors.target}
                    </p>
                  )}
                  {errors.minimum && (
                    <p id="habit-minimum-error" className="text-xs text-destructive" data-testid="minimum-error">
                      {errors.minimum}
                    </p>
                  )}
                </div>
              )}
            </div>
          )}

          <fieldset>
            <legend className="text-sm font-medium">Icon</legend>
            <div className="mt-1 flex flex-wrap gap-2">
              {HABIT_ICON_KEYS.map((key) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => update('icon', key)}
                  aria-pressed={values.icon === key}
                  aria-label={key.replace('-', ' ')}
                  className={cn(
                    'flex h-11 w-11 items-center justify-center rounded-md border',
                    values.icon === key ? 'border-primary bg-primary/10 text-foreground' : 'border-border text-muted-foreground'
                  )}
                >
                  <HabitIcon icon={key} type={values.type} className="h-5 w-5" />
                </button>
              ))}
            </div>
          </fieldset>

          <DialogFooter className="flex-col gap-2 sm:flex-row sm:justify-between">
            {habit && onArchive ? (
              confirmArchive ? (
                <div className="flex gap-2">
                  <Button type="button" variant="destructive" className="h-12" onClick={() => onArchive(habit, !habit.archived)}>
                    {habit.archived ? 'Confirm restore' : 'Confirm archive'}
                  </Button>
                  <Button type="button" variant="ghost" className="h-12" onClick={() => setConfirmArchive(false)}>
                    Keep
                  </Button>
                </div>
              ) : (
                <Button type="button" variant="ghost" className="h-12 text-muted-foreground" onClick={() => setConfirmArchive(true)}>
                  {habit.archived ? <ArchiveRestore aria-hidden="true" /> : <Archive aria-hidden="true" />}
                  {habit.archived ? 'Restore' : 'Archive'}
                </Button>
              )
            ) : (
              <span />
            )}
            <div className="flex gap-2">
              <Button type="button" variant="outline" className="h-12" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" className="h-12" disabled={saving}>
                {saving ? 'Saving…' : 'Save'}
              </Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
