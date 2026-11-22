/**
 * Your own mobility, stretch and yoga routines: play one at home, edit it, or make a new one.
 * Reached from the Plan tab and from a mobility card's Play button on Today.
 */

import { Link } from 'react-router-dom';
import { Pencil, Play, Plus } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { estimateMinutes } from './routinePlayer';

export function RoutinesPage() {
  const { data: routines, isLoading, isError, refetch } = useMobilityRoutines();

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header className="flex items-center justify-between gap-3">
          <h1 className="text-2xl font-bold text-foreground">Routines</h1>
          <Button asChild data-testid="new-routine">
            <Link to="/routines/new">
              <Plus aria-hidden="true" />
              New routine
            </Link>
          </Button>
        </header>
        <p className="text-sm text-muted-foreground">Mobility, stretching and yoga. Play one at home and it counts for today.</p>

        {isLoading && <p className="text-muted-foreground">Loading routines…</p>}

        {isError && (
          <div role="alert" className="space-y-3 rounded-lg border border-destructive/40 p-4">
            <p className="text-foreground">Couldn't load your routines.</p>
            <Button variant="outline" onClick={() => refetch()}>
              Try again
            </Button>
          </div>
        )}

        {routines && routines.length === 0 && (
          <div className="rounded-lg border border-dashed border-border p-6 text-center">
            <p className="font-medium text-foreground">No routines yet</p>
            <p className="mt-1 text-sm text-muted-foreground">Build your evening stretch or a yoga flow, drill by drill.</p>
          </div>
        )}

        {routines && routines.length > 0 && (
          <ul className="space-y-3">
            {routines.map((routine) => (
              <li key={routine.id}>
                <article className="rounded-lg border border-border bg-card p-4" aria-label={routine.name}>
                  <h2 className="text-lg font-semibold text-foreground">{routine.name}</h2>
                  <p className="text-sm text-muted-foreground">
                    {routine.drills.length} {routine.drills.length === 1 ? 'drill' : 'drills'} · about {estimateMinutes(routine)} min
                  </p>
                  <div className="mt-3 grid grid-cols-2 gap-2">
                    <Button asChild size="lg" className="w-full">
                      <Link to={`/routines/${routine.id}/play`} aria-label={`Play ${routine.name}`}>
                        <Play aria-hidden="true" />
                        Play
                      </Link>
                    </Button>
                    <Button asChild size="lg" variant="outline" className="w-full">
                      <Link to={`/routines/${routine.id}/edit`} aria-label={`Edit ${routine.name}`}>
                        <Pencil aria-hidden="true" />
                        Edit
                      </Link>
                    </Button>
                  </div>
                </article>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}
