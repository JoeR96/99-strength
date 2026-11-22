/**
 * "Up next" on Today: on a rest day, or once today's sessions are done, the next day (within 14)
 * that has a session to do, with a one-line summary of each session and a Preview button.
 * Renders nothing while loading, on error, or with no plan.
 */

import { CalendarClock, Eye } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useUpcomingDays } from '@/hooks/usePlan';
import { addDays, type LocalDate } from '@/lib/localDate';
import { SessionKind, type TodaySessionDto } from '@/types/plan';
import { SessionSummary } from './SessionSummary';
import { dayHeading, pickUpNext } from './upNext';

export const UP_NEXT_DAYS = 14;

const kindName: Record<SessionKind, string> = {
  [SessionKind.Rest]: 'Rest',
  [SessionKind.Lift]: 'Lift',
  [SessionKind.Cardio]: 'Cardio',
  [SessionKind.Mobility]: 'Mobility',
};

export interface UpNextCardProps {
  today: LocalDate;
  onPreview: (session: TodaySessionDto, date: LocalDate) => void;
}

export function UpNextCard({ today, onPreview }: UpNextCardProps) {
  const upcoming = useUpcomingDays(addDays(today, 1), UP_NEXT_DAYS);
  if (!upcoming.data) return null;
  const next = pickUpNext(upcoming.data);

  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby="up-next-heading" data-testid="up-next">
      <div className="flex items-center gap-2">
        <CalendarClock className="h-5 w-5 shrink-0 text-primary" aria-hidden="true" />
        <h2 id="up-next-heading" className="text-sm font-medium text-muted-foreground">
          Up next
        </h2>
      </div>
      {next === null ? (
        <p className="mt-2 text-sm text-foreground">Nothing planned in the next two weeks.</p>
      ) : (
        <>
          <p className="mt-1 text-lg font-semibold text-foreground" data-testid="up-next-date">
            {dayHeading(next.date, today)}
          </p>
          <ul className="mt-3 space-y-3">
            {next.sessions.map((session, i) => (
              <li key={`${session.kind}-${session.label}-${i}`} className="flex items-center gap-3">
                <div className="min-w-0 flex-1">
                  <p className="text-xs font-medium text-muted-foreground">{kindName[session.kind]}</p>
                  <p className="truncate font-semibold text-foreground">{session.label}</p>
                  <SessionSummary session={session} date={next.date} lifts={next.lifts} />
                </div>
                <Button
                  variant="outline"
                  className="h-11 shrink-0"
                  onClick={() => onPreview(session, next.date)}
                  aria-label={`Preview ${session.label}`}
                  data-testid="up-next-preview"
                >
                  <Eye aria-hidden="true" />
                  Preview
                </Button>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
