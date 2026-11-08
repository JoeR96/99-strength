/** "Paused — holiday until 3 Jan": the current phase and when it ends. */

import { Pause, Scale, Snowflake, TrendingDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatShortDate } from '@/lib/localDate';
import { PhaseType, phaseTypeLabel, type PlanPhaseDto } from '@/types/plan';

export interface PhaseBannerProps {
  phase: PhaseType | null;
  /** The matching plan phase, when known, for its end date and note. */
  detail?: PlanPhaseDto | null;
}

const icons: Record<PhaseType, typeof Pause> = {
  [PhaseType.Cut]: TrendingDown,
  [PhaseType.Maintenance]: Scale,
  [PhaseType.Pause]: Pause,
  [PhaseType.Deload]: Snowflake,
};

export function PhaseBanner({ phase, detail }: PhaseBannerProps) {
  if (phase === null) return null;
  const Icon = icons[phase];
  const isPause = phase === PhaseType.Pause;
  const note = detail?.note?.trim();
  const until = detail ? `until ${formatShortDate(detail.to)}` : null;
  const text = [phaseTypeLabel[phase], [note ? note.toLowerCase() : null, until].filter(Boolean).join(' ')]
    .filter(Boolean)
    .join(' — ');

  return (
    <div
      role="status"
      className={cn(
        'flex items-center gap-2 rounded-lg border px-3 py-2 text-sm',
        isPause ? 'border-warning/50 bg-warning/10 text-warning' : 'border-border bg-card text-muted-foreground'
      )}
      data-testid="phase-banner"
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{text}</span>
    </div>
  );
}
