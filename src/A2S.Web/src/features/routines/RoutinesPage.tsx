/**
 * Mobility (`/routines`, its own tab): your mobility, stretch and yoga routines grouped by kind
 * (Warm-ups, Corrective, Stretch, Yoga flow, Follow-along). Each card shows minutes and drill count,
 * with Play, Preview (every pose with its dose, cues and Easier/Harder versions) and Edit.
 */

import { Link } from 'react-router-dom';
import { Eye, Pencil, Play, Plus } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { MobilityRoutineKind, type MobilityRoutineDto } from '@/types/mobility';
import { groupRoutines } from './routineGroups';
import { estimateMinutes } from './routinePlayer';

function RoutineCard({ routine }: { routine: MobilityRoutineDto }) {
  const isVideo = routine.kind === MobilityRoutineKind.FollowAlong;
  return (
    <article className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-border bg-card p-4" aria-label={routine.name}>
      <div className="min-w-0 flex-1 basis-40">
      <h3 className="truncate text-lg font-semibold text-foreground">{routine.name}</h3>
      <p className="text-sm text-muted-foreground">
        {isVideo
          ? `Follow-along video · ${routine.targetMinutes} min`
          : `${routine.drills.length} ${routine.drills.length === 1 ? 'drill' : 'drills'} · about ${estimateMinutes(routine)} min`}
      </p>
      </div>
      <div className="flex shrink-0 items-center gap-1">
        <Button asChild variant="tinted" className="h-11 rounded-full px-5">
          <Link to={`/routines/${routine.id}/play`} aria-label={`Play ${routine.name}`}>
            <Play aria-hidden="true" />
            Play
          </Link>
        </Button>
        <Button asChild size="icon" variant="ghost" className="h-11 w-11 rounded-full">
          <Link to={`/routines/${routine.id}`} aria-label={`Preview ${routine.name}`}>
            <Eye aria-hidden="true" />
          </Link>
        </Button>
        <Button asChild size="icon" variant="ghost" className="h-11 w-11 rounded-full">
          <Link to={`/routines/${routine.id}/edit`} aria-label={`Edit ${routine.name}`}>
            <Pencil aria-hidden="true" />
          </Link>
        </Button>
      </div>
    </article>
  );
}

export function RoutinesPage() {
  const { data: routines, isLoading, isError, refetch } = useMobilityRoutines();

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header className="flex items-center justify-between gap-3">
          <h1 className="text-hero">Mobility</h1>
          <Button asChild className="h-11" data-testid="new-routine">
            <Link to="/routines/new">
              <Plus aria-hidden="true" />
              New routine
            </Link>
          </Button>
        </header>
        <p className="text-sm text-muted-foreground">Warm-ups, stretching and yoga. Preview the poses and cues, then press Play.</p>

        {isLoading && <p className="text-muted-foreground">Loading routines…</p>}

        {isError && (
          <div role="alert" className="space-y-3 rounded-lg border border-destructive/40 p-4">
            <p className="text-foreground">Couldn't load your routines.</p>
            <Button variant="outline" className="h-11" onClick={() => refetch()}>
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

        {routines &&
          groupRoutines(routines).map((group) => (
            <section key={group.kind} aria-labelledby={`routine-group-${group.kind}`} className="space-y-2" data-testid={`routine-group-${group.kind}`}>
              <h2 id={`routine-group-${group.kind}`} className="text-section">
                {group.heading}
              </h2>
              <ul className="space-y-3">
                {group.routines.map((routine) => (
                  <li key={routine.id}>
                    <RoutineCard routine={routine} />
                  </li>
                ))}
              </ul>
            </section>
          ))}
      </main>
    </div>
  );
}
