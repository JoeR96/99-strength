/**
 * Today's one-tap readiness (brief § 5d): Green / Yellow / Red, the WHOOP recovery colour.
 * Red suggests skipping the lift slot's finisher and hard cardio (walks and home routines stay),
 * with one tap to keep the finisher. Yellow is a gentle note. Nothing here changes the plan's maths.
 */

import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { LocalDate } from '@/lib/localDate';
import { useReadiness, useSetReadiness, useSetReadinessFinisher } from '@/hooks/useReadiness';
import type { ReadinessColour, ReadinessDto } from '@/types/readiness';

const colours: { value: ReadinessColour; label: string; selected: string }[] = [
  { value: 'Green', label: 'Green', selected: 'bg-success text-success-foreground border-success' },
  { value: 'Yellow', label: 'Yellow', selected: 'bg-warning text-warning-foreground border-warning' },
  { value: 'Red', label: 'Red', selected: 'bg-destructive text-destructive-foreground border-destructive' },
];

export interface ReadinessCardProps {
  readiness: ReadinessDto;
  onSelect: (colour: ReadinessColour) => void;
  onKeepFinisher: (keep: boolean) => void;
  busy?: boolean;
}

export function ReadinessCard({ readiness, onSelect, onKeepFinisher, busy = false }: ReadinessCardProps) {
  const red = readiness.colour === 'Red';
  return (
    <section aria-labelledby="readiness-heading" className="space-y-3 rounded-lg border border-border bg-card p-4" data-testid="readiness-card">
      <h2 id="readiness-heading" className="text-sm font-medium text-muted-foreground">
        Recovery today
      </h2>
      <div className="grid grid-cols-3 gap-2" role="group" aria-label="Recovery colour">
        {colours.map((c) => {
          const active = readiness.colour === c.value;
          return (
            <Button
              key={c.value}
              variant="outline"
              className={cn('h-11', active && c.selected)}
              aria-pressed={active}
              disabled={busy}
              onClick={() => onSelect(c.value)}
            >
              {c.label}
            </Button>
          );
        })}
      </div>

      {readiness.headline && (
        <div role="status" className="space-y-1">
          <p className="text-sm font-semibold text-foreground">{readiness.headline}</p>
          <ul className="list-disc space-y-1 pl-5 text-sm text-foreground">
            {readiness.suggestions.map((s) => (
              <li key={s}>{s}</li>
            ))}
          </ul>
        </div>
      )}

      {red && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-border p-3">
          <p className="text-sm text-foreground" data-testid="finisher-status">
            {readiness.finisherSkipped ? 'Finisher: Skipped for recovery' : 'Finisher: kept'}
          </p>
          <Button
            variant="outline"
            className="h-11 shrink-0"
            disabled={busy}
            onClick={() => onKeepFinisher(readiness.finisherSkipped)}
          >
            {readiness.finisherSkipped ? 'Undo' : 'Skip it'}
          </Button>
        </div>
      )}
    </section>
  );
}

/** Loads and saves the day's readiness. Renders nothing while loading or on error, so Today never waits on it. */
export function TodayReadiness({ date }: { date: LocalDate }) {
  const query = useReadiness(date);
  const set = useSetReadiness();
  const finisher = useSetReadinessFinisher();
  if (!query.data) return null;

  const fail = () => toast.error("Couldn't save that. Try again.");
  return (
    <ReadinessCard
      readiness={query.data}
      busy={set.isPending || finisher.isPending}
      onSelect={(colour) => set.mutate({ date, colour }, { onError: fail })}
      onKeepFinisher={(keep) => finisher.mutate({ date, keep }, { onError: fail })}
    />
  );
}
