/** The habit streak. Amber when today is only alive thanks to the one-miss grace. */

import { Flame } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { HabitStreakDto } from '@/types/habits';

export function StreakChip({ streak }: { streak: HabitStreakDto }) {
  const grace = streak.graceUsed;
  const label = grace
    ? `${streak.current}-day streak, grace day used`
    : `${streak.current}-day streak, best ${streak.best}`;
  return (
    <span
      className={cn(
        'inline-flex h-9 items-center gap-1.5 rounded-full border px-3 text-sm font-medium tabular-nums',
        grace ? 'border-warning/50 bg-warning/10 text-warning' : 'border-border bg-card text-foreground'
      )}
      aria-label={label}
      title={label}
      data-testid="streak-chip"
    >
      <Flame className={cn('h-4 w-4', grace ? 'text-warning' : 'text-primary')} aria-hidden="true" />
      {streak.current}
      {grace && <span className="text-xs font-normal">grace</span>}
    </span>
  );
}
