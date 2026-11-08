/** First run: no plan yet. Phase 7 replaces this with the "Start this plan" onboarding card. */

import { Link } from 'react-router-dom';
import { CalendarDays } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function NoPlanCard() {
  return (
    <section className="rounded-lg border border-dashed border-border bg-card p-6 text-center" data-testid="no-plan-card">
      <CalendarDays className="mx-auto mb-3 h-10 w-10 text-muted-foreground" aria-hidden="true" />
      <h2 className="text-xl font-semibold text-foreground">No plan yet</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        Start a plan to see today's session here. Habits and your weigh-in work already.
      </p>
      <Button asChild size="lg" className="mt-4 w-full">
        <Link to="/setup">Start a plan</Link>
      </Button>
    </section>
  );
}
