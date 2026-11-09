/**
 * Today's habits as editable rows: counters get − / +, ticks toggle, numbers
 * take a value. Every control is at least 44 px; the pencil opens the edit sheet.
 */

import { useState } from 'react';
import { Check, Minus, Pencil, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import type { HabitTodayDto } from '@/types/habits';
import { HabitIcon } from './HabitIcon';

export interface HabitListProps {
  habits: HabitTodayDto[];
  onTap: (habitId: string, delta: 1 | -1) => void;
  onSetValue: (habitId: string, value: number) => void;
  onEdit: (habitId: string) => void;
}

function NumericControl({ habit, onSetValue }: { habit: HabitTodayDto; onSetValue: (id: string, value: number) => void }) {
  const [draft, setDraft] = useState('');
  const [editing, setEditing] = useState(habit.value <= 0);
  const submit = () => {
    const n = Number(draft.trim().replace(',', '.'));
    if (!Number.isFinite(n) || draft.trim() === '') return;
    onSetValue(habit.id, n);
    setEditing(false);
    setDraft('');
  };
  if (!editing) {
    return (
      <Button type="button" variant="outline" size="sm" className="h-11" onClick={() => setEditing(true)}>
        {habit.value}
        {habit.unit ? ` ${habit.unit}` : ''}
      </Button>
    );
  }
  return (
    <form
      className="flex items-center gap-1"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <Input
        inputMode="decimal"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        aria-label={`${habit.name} value`}
        placeholder={habit.unit ?? ''}
        className="h-11 w-20"
      />
      <Button type="submit" size="sm" className="h-11">
        Set
      </Button>
    </form>
  );
}

export function HabitList({ habits, onTap, onSetValue, onEdit }: HabitListProps) {
  if (habits.length === 0) {
    return (
      <p className="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground" data-testid="habit-list-empty">
        No habits yet. Add your first one below.
      </p>
    );
  }
  return (
    <ul className="space-y-2" data-testid="habit-list">
      {habits.map((habit) => {
        const done = habit.value >= 1;
        return (
          <li
            key={habit.id}
            className={cn('flex items-center gap-3 rounded-lg border bg-card p-3', habit.metTarget ? 'border-success/60' : 'border-border')}
            data-testid={`habit-row-${habit.id}`}
          >
            <HabitIcon icon={habit.icon} type={habit.type} className="h-6 w-6 shrink-0 text-muted-foreground" />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5">
                <span className="truncate text-sm font-medium text-foreground">{habit.name}</span>
                {habit.metMinimum && (
                  <Check className={cn('h-4 w-4 shrink-0', habit.metTarget ? 'text-success' : 'text-warning')} aria-hidden="true" />
                )}
              </div>
              <div className="text-xs text-muted-foreground">
                {habit.type === 'Checkbox'
                  ? done
                    ? 'Done today'
                    : 'Not yet'
                  : `${habit.value} / ${habit.target}${habit.unit ? ` ${habit.unit}` : ''} · min ${habit.minimum}`}
              </div>
            </div>

            {habit.type === 'Counter' && (
              <div className="flex shrink-0 items-center gap-1">
                <button
                  type="button"
                  onClick={() => onTap(habit.id, -1)}
                  disabled={habit.value <= 0}
                  aria-label={`One less ${habit.name}`}
                  className="flex h-11 w-11 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-muted disabled:opacity-40"
                >
                  <Minus className="h-5 w-5" aria-hidden="true" />
                </button>
                <button
                  type="button"
                  onClick={() => onTap(habit.id, 1)}
                  aria-label={`Add one ${habit.name}`}
                  className="flex h-11 w-11 items-center justify-center rounded-md bg-primary text-primary-foreground"
                >
                  <Plus className="h-5 w-5" aria-hidden="true" />
                </button>
              </div>
            )}

            {habit.type === 'Checkbox' && (
              <button
                type="button"
                role="checkbox"
                aria-checked={done}
                aria-label={habit.name}
                onClick={() => onSetValue(habit.id, done ? 0 : 1)}
                className={cn(
                  'flex h-11 w-11 shrink-0 items-center justify-center rounded-full border-2',
                  done ? 'border-success bg-success text-success-foreground' : 'border-border text-transparent'
                )}
              >
                <Check className="h-6 w-6" strokeWidth={3} aria-hidden="true" />
              </button>
            )}

            {habit.type === 'Numeric' && <NumericControl habit={habit} onSetValue={onSetValue} />}

            <button
              type="button"
              onClick={() => onEdit(habit.id)}
              aria-label={`Edit ${habit.name}`}
              className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <Pencil className="h-4 w-4" aria-hidden="true" />
            </button>
          </li>
        );
      })}
    </ul>
  );
}
