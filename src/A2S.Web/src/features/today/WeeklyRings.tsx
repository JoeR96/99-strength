/**
 * The week's goal rings: lifts, cardio, mobility, flexible meal and alcohol.
 * Session counts come from `WeeklyGoalsDto`, which already leaves paused sessions
 * out of the planned totals; a ring with nothing planned shows a dash.
 */

import { cn } from '@/lib/utils';
import type { WeeklyGoalsDto } from '@/types/plan';
import { ringsFor, type RingDefinition } from './rings';

const toneStroke: Record<RingDefinition['tone'], string> = {
  primary: 'var(--color-primary)',
  success: 'var(--color-success)',
  warning: 'var(--color-warning)',
  muted: 'var(--color-muted-foreground)',
  over: 'var(--color-success)',
};

function Ring({ ring }: { ring: RingDefinition }) {
  const size = 48;
  const stroke = 5;
  const radius = (size - stroke) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference * (1 - ring.progress);
  const Icon = ring.icon;
  const label =
    ring.tone === 'over'
      ? `${ring.label}: ${ring.done}, over the weekly maximum`
      : ring.planned === 0
        ? `${ring.label}: nothing planned`
        : `${ring.label}: ${ring.done} of ${ring.planned}`;

  return (
    <li className="flex flex-col items-center gap-1" aria-label={label}>
      <div className="relative" style={{ width: size, height: size }}>
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true">
          <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--color-border)" strokeWidth={stroke} />
          {ring.tone === 'over' && (
            <circle
              cx={size / 2}
              cy={size / 2}
              r={size / 2 - 1}
              fill="none"
              stroke="var(--color-muted-foreground)"
              strokeWidth={2}
              strokeDasharray="3 3"
              data-testid={`ring-${ring.key}-over`}
            />
          )}
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={toneStroke[ring.tone]}
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            transform={`rotate(-90 ${size / 2} ${size / 2})`}
            style={{ transition: 'stroke-dashoffset 300ms ease' }}
          />
        </svg>
        <Icon
          className={cn('absolute inset-0 m-auto h-5 w-5', ring.tone === 'muted' ? 'text-muted-foreground' : 'text-foreground')}
          aria-hidden="true"
        />
      </div>
      <span className="text-xs font-medium tabular-nums text-foreground" data-testid={`ring-${ring.key}`}>
        {ring.text ?? (ring.planned === 0 ? '—' : `${ring.done}/${ring.planned}`)}
      </span>
      <span className="text-[11px] text-muted-foreground">{ring.label}</span>
    </li>
  );
}

export function WeeklyRings({ week }: { week: WeeklyGoalsDto }) {
  return (
    <section aria-labelledby="weekly-rings-heading" className="rounded-lg border border-border bg-card p-4">
      <h2 id="weekly-rings-heading" className="mb-3 text-sm font-medium text-muted-foreground">
        This week
      </h2>
      <ul className="grid grid-cols-6 gap-1">
        {ringsFor(week).map((ring) => (
          <Ring key={ring.key} ring={ring} />
        ))}
      </ul>
    </section>
  );
}
