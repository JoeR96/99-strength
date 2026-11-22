/**
 * Routine preview (`/routines/:id`): every pose before you press Play: its dose, cues, the standing
 * alternative, and the Easier / Harder versions (the drill's levels, current one marked). Read-only.
 */

import { Link, useParams } from 'react-router-dom';
import { ExternalLink, Pencil, Play } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { MobilityRoutineKind, routineKindLabel, type MobilityDrillDto } from '@/types/mobility';
import { describeDrill, estimateMinutes } from './routinePlayer';
import { levelLabel, levelNeighbours, shortDose } from './drillLevels';

function DrillPreview({ drill, index }: { drill: MobilityDrillDto; index: number }) {
  const levels = drill.levels ?? [];
  const at = drill.currentLevel ?? 0;
  const { easier, harder } = levelNeighbours(drill);
  return (
    <li className="rounded-lg border border-border bg-card p-4" data-testid={`preview-drill-${index}`}>
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="min-w-0 text-lg font-semibold text-foreground">
          {index + 1}. {drill.name}
        </h2>
        {levels[at] && <span className="shrink-0 text-sm text-muted-foreground">{levels[at].name}</span>}
      </div>
      <p className="mt-1 font-medium text-foreground">{levels[at] ? `${levels[at].description ?? levels[at].name} · ${shortDose(levels[at].sets, levels[at].dose)}` : describeDrill(drill)}</p>
      {(levels[at]?.cues ?? drill.cues) && <p className="mt-1 text-sm text-muted-foreground">Cues: {levels[at]?.cues ?? drill.cues}</p>}
      {drill.standingAlternative && <p className="mt-1 text-sm text-muted-foreground">Standing: {drill.standingAlternative}</p>}
      {(easier || harder) && (
        <dl className="mt-2 space-y-1 text-sm">
          {easier && (
            <div className="flex gap-2">
              <dt className="w-14 shrink-0 text-muted-foreground">Easier</dt>
              <dd className="text-foreground">{levelLabel(easier)}</dd>
            </div>
          )}
          {harder && (
            <div className="flex gap-2">
              <dt className="w-14 shrink-0 text-muted-foreground">Harder</dt>
              <dd className="text-foreground">{levelLabel(harder)}</dd>
            </div>
          )}
        </dl>
      )}
    </li>
  );
}

export function RoutinePreviewPage() {
  const { id } = useParams<{ id: string }>();
  const { data: routines, isLoading, isError, refetch } = useMobilityRoutines();
  const routine = routines?.find((r) => r.id === id) ?? null;

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        {isLoading && <p className="text-muted-foreground">Loading the routine…</p>}
        {isError && (
          <div role="alert" className="space-y-3 rounded-lg border border-destructive/40 p-4">
            <p className="text-foreground">Couldn't load the routine.</p>
            <Button variant="outline" className="h-11" onClick={() => refetch()}>
              Try again
            </Button>
          </div>
        )}
        {routines && !routine && (
          <div className="rounded-lg border border-dashed border-border p-6 text-center">
            <p className="font-medium text-foreground">Routine not found</p>
            <Link to="/routines" className="mt-2 inline-flex min-h-11 items-center text-sm font-medium text-primary">
              Back to Mobility
            </Link>
          </div>
        )}
        {routine && (
          <>
            <header>
              <p className="text-sm text-muted-foreground">
                {routineKindLabel[routine.kind ?? MobilityRoutineKind.Stretch]} · about {routine.kind === MobilityRoutineKind.FollowAlong ? routine.targetMinutes : estimateMinutes(routine)} min
              </p>
              <h1 className="text-2xl font-bold text-foreground">{routine.name}</h1>
            </header>
            {routine.followAlongUrl && (
              <a
                href={routine.followAlongUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex min-h-11 items-center gap-1 text-sm font-medium text-primary underline-offset-4 hover:underline"
              >
                <ExternalLink className="h-4 w-4" aria-hidden="true" />
                Open the video
              </a>
            )}
            <ol className="space-y-3" aria-label="Poses">
              {routine.drills.map((drill, i) => (
                <DrillPreview key={`${drill.name}-${i}`} drill={drill} index={i} />
              ))}
            </ol>
            <div className="sticky bottom-20 grid grid-cols-[1fr_auto] gap-2 pb-4">
              <Button asChild size="lg" className="h-14 text-lg" data-testid="preview-play">
                <Link to={`/routines/${routine.id}/play`}>
                  <Play aria-hidden="true" />
                  Play
                </Link>
              </Button>
              <Button asChild size="lg" variant="outline" className="h-14" aria-label={`Edit ${routine.name}`}>
                <Link to={`/routines/${routine.id}/edit`}>
                  <Pencil aria-hidden="true" />
                </Link>
              </Button>
            </div>
          </>
        )}
      </main>
    </div>
  );
}
