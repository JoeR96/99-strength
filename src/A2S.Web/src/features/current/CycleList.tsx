/**
 * "Up next in the cycle": the rest of this A2S2 week as compact rows, and "Preview next week" to
 * show the next week's sessions. Each row opens the read-only gym preview for that week and day.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { CycleSessionDto } from '@/api/workouts';
import { previewHref } from './cycle';

function Row({ session }: { session: CycleSessionDto }) {
  const main = session.plan.exercises.find((e) => !e.cardioBlock)?.name;
  const cardio = session.plan.exercises.filter((e) => e.cardioBlock).map((e) => e.name);
  if (session.plan.cardio) cardio.push(session.plan.cardio.name);
  return (
    <li>
      <Link
        to={previewHref(session)}
        className="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 transition-colors hover:bg-foreground/[0.05] active:bg-foreground/[0.08]"
        data-testid={`cycle-row-${session.week}-${session.day}`}
      >
        <span className="w-14 shrink-0 text-xs text-muted-foreground">W{session.week} · {session.position}</span>
        <span className="min-w-0 flex-1">
          <span className="block truncate font-medium text-foreground">{session.label}</span>
          {(main || cardio.length > 0) && <span className="block truncate text-xs text-muted-foreground">{[main, ...cardio].filter(Boolean).join(' · ')}</span>}
        </span>
        {session.isDeloadWeek && <span className="text-xs text-muted-foreground">Deload</span>}
        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
      </Link>
    </li>
  );
}

export function CycleList({ thisWeek, nextWeek }: { thisWeek: CycleSessionDto[]; nextWeek: CycleSessionDto[] }) {
  const [showNextWeek, setShowNextWeek] = useState(thisWeek.length === 0);
  if (thisWeek.length === 0 && nextWeek.length === 0) return null;
  return (
    <section className="rounded-xl border border-border bg-card p-2" aria-labelledby="cycle-heading" data-testid="cycle-list">
      <h2 id="cycle-heading" className="px-3 pt-2 text-section">
        Up next in the cycle
      </h2>
      {thisWeek.length > 0 && <ul>{thisWeek.map((s) => <Row key={`${s.week}-${s.day}`} session={s} />)}</ul>}
      {nextWeek.length > 0 && (
        <>
          <Button
            type="button"
            variant="ghost"
            className="h-11 w-full justify-between px-3"
            onClick={() => setShowNextWeek((v) => !v)}
            aria-expanded={showNextWeek}
            data-testid="preview-next-week"
          >
            Preview next week (week {nextWeek[0].week})
            <ChevronDown className={showNextWeek ? 'rotate-180' : undefined} aria-hidden="true" />
          </Button>
          {showNextWeek && <ul>{nextWeek.map((s) => <Row key={`${s.week}-${s.day}`} session={s} />)}</ul>}
        </>
      )}
    </section>
  );
}
