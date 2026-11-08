/** A checkbox habit on the Today screen: one 64 px toggle. */

import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { HabitTodayDto } from '@/types/habits';
import { HabitIcon } from '@/features/habits/HabitIcon';

export interface HabitTickProps {
  habit: HabitTodayDto;
  onToggle: (habitId: string, done: boolean) => void;
  pending?: boolean;
}

export function HabitTick({ habit, onToggle, pending = false }: HabitTickProps) {
  const done = habit.value >= 1;
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={done}
      onClick={() => onToggle(habit.id, !done)}
      className={cn(
        'flex min-h-16 w-full items-center gap-3 rounded-lg border bg-card p-3 text-left transition-colors',
        done ? 'border-success/60' : 'border-border hover:bg-muted/40'
      )}
      data-testid={`habit-tick-${habit.id}`}
    >
      <HabitIcon icon={habit.icon} type={habit.type} className="h-6 w-6 shrink-0 text-muted-foreground" />
      <span className="min-w-0 flex-1 truncate text-sm font-medium text-foreground">{habit.name}</span>
      {pending && <span className="h-2 w-2 shrink-0 rounded-full bg-warning" aria-label="Not yet synced" />}
      <span
        className={cn(
          'flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2',
          done ? 'border-success bg-success text-success-foreground' : 'border-border text-transparent'
        )}
        aria-hidden="true"
      >
        <Check className="h-6 w-6" strokeWidth={3} />
      </span>
    </button>
  );
}
