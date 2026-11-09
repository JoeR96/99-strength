/**
 * One counter habit on the Today screen. The `+` is the primary 64 px target;
 * holding it for 500 ms takes one off, as does the small `−` button for anyone
 * who would rather not long-press. The value reads "3 / 6" with the unit under it
 * and a tick once the minimum is met (filled when the target is met).
 */

import { useRef, type PointerEvent } from 'react';
import { Check, Minus, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { HabitTodayDto } from '@/types/habits';
import { HabitIcon } from '@/features/habits/HabitIcon';

export const LONG_PRESS_MS = 500;

export interface HabitCounterProps {
  habit: HabitTodayDto;
  onTap: (habitId: string, delta: 1 | -1) => void;
  /** Shown while the tap has not been confirmed (PR 7 sets this while offline). */
  pending?: boolean;
}

export function HabitCounter({ habit, onTap, pending = false }: HabitCounterProps) {
  const holdTimer = useRef<number | null>(null);
  const longPressFired = useRef(false);

  const clearHold = () => {
    if (holdTimer.current !== null) {
      window.clearTimeout(holdTimer.current);
      holdTimer.current = null;
    }
  };

  const onPointerDown = (event: PointerEvent<HTMLButtonElement>) => {
    if (event.button !== 0 && event.pointerType === 'mouse') return;
    longPressFired.current = false;
    clearHold();
    holdTimer.current = window.setTimeout(() => {
      longPressFired.current = true;
      holdTimer.current = null;
      if (habit.value > 0) onTap(habit.id, -1);
    }, LONG_PRESS_MS);
  };

  const onClick = () => {
    clearHold();
    if (longPressFired.current) {
      longPressFired.current = false;
      return;
    }
    onTap(habit.id, 1);
  };

  const canDecrement = habit.value > 0;
  const formattedValue = Number.isInteger(habit.value) ? habit.value : habit.value.toFixed(1);

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg border border-border bg-card p-3',
        habit.metTarget && 'border-success/60'
      )}
      data-testid={`habit-counter-${habit.id}`}
    >
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <HabitIcon icon={habit.icon} type={habit.type} className="h-6 w-6 shrink-0 text-muted-foreground" />
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="truncate text-sm font-medium text-foreground">{habit.name}</span>
            {habit.metMinimum && (
              <Check
                className={cn('h-4 w-4 shrink-0', habit.metTarget ? 'text-success' : 'text-warning')}
                aria-label={habit.metTarget ? 'Target met' : 'Minimum met'}
              />
            )}
            {pending && <span className="h-2 w-2 shrink-0 rounded-full bg-warning" aria-label="Not yet synced" />}
          </div>
          <div className="text-2xl font-semibold tabular-nums text-foreground" aria-live="polite">
            {formattedValue}
            <span className="text-base font-normal text-muted-foreground"> / {habit.target}</span>
          </div>
          {habit.unit && <div className="text-xs text-muted-foreground">{habit.unit}</div>}
        </div>
      </div>

      <div className="flex shrink-0 items-center gap-2">
        <button
          type="button"
          onClick={() => canDecrement && onTap(habit.id, -1)}
          disabled={!canDecrement}
          aria-label={`One less ${habit.name}`}
          className="flex h-11 w-11 items-center justify-center rounded-md border border-border text-muted-foreground transition-colors hover:bg-muted disabled:opacity-40"
        >
          <Minus className="h-5 w-5" aria-hidden="true" />
        </button>
        <button
          type="button"
          onPointerDown={onPointerDown}
          onPointerUp={clearHold}
          onPointerLeave={clearHold}
          onPointerCancel={clearHold}
          onContextMenu={(e) => e.preventDefault()}
          onClick={onClick}
          aria-label={`Add one ${habit.name}`}
          title="Hold to take one off"
          className="flex h-16 w-16 select-none items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm transition-transform active:scale-95 [touch-action:manipulation]"
        >
          <Plus className="h-8 w-8" strokeWidth={2.5} aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}
