/**
 * Accepting progress: a pending "move up a level" suggestion with Accept / Not yet, and the routine's progress
 * section (each levelled drill's level and dose, pending suggestions, the last 10 changes with reasons, and
 * "Undo last change"). Accept / decline / undo are online-only; offline they say so instead of failing quietly.
 */

import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { useRespondToDrillSuggestion, useUndoDrillProgression } from '@/hooks/useMobilityRoutines';
import type { MobilityDrillDto, MobilityRoutineDto } from '@/types/mobility';
import { levelLabel, prescription } from './drillLevels';

function failureMessage(action: string): string {
  return typeof navigator !== 'undefined' && navigator.onLine === false
    ? `You're offline. ${action} when you're back online.`
    : `Couldn't ${action.toLowerCase()}. Try again.`;
}

/** "Move up to Level 2: Hands on a bench, straight legs?" with Accept / Not yet. Renders nothing without a suggestion. */
export function LevelSuggestionPrompt({ routineId, drill }: { routineId: string; drill: MobilityDrillDto }) {
  const respond = useRespondToDrillSuggestion();
  const target = drill.suggestedLevel != null ? drill.levels?.[drill.suggestedLevel] : undefined;
  if (!target) return null;

  const answer = (accept: boolean) =>
    respond.mutate(
      { id: routineId, drillName: drill.name, accept },
      {
        onSuccess: () => toast.success(accept ? `${drill.name}: moved up to ${target.name}` : `${drill.name}: staying where you are`),
        onError: () => toast.error(failureMessage(accept ? 'Accept the new level' : 'Answer the suggestion')),
      }
    );

  return (
    <div className="space-y-2 rounded-lg border border-primary/50 p-3" data-testid="level-suggestion">
      <p className="text-base font-medium text-foreground">
        {drill.name}: move up to {levelLabel(target)}?
      </p>
      <div className="grid grid-cols-2 gap-2">
        <Button type="button" className="h-11" disabled={respond.isPending} onClick={() => answer(true)}>
          Accept
        </Button>
        <Button type="button" variant="outline" className="h-11" disabled={respond.isPending} onClick={() => answer(false)}>
          Not yet
        </Button>
      </div>
    </div>
  );
}

/** Progress for a routine whose drills have levels; nothing for a routine without any. */
export function RoutineProgress({ routine }: { routine: MobilityRoutineDto }) {
  const undo = useUndoDrillProgression();
  const levelled = routine.drills.filter((d) => (d.levels?.length ?? 0) > 0);
  const changes = (routine.recentChanges ?? []).slice(0, 10);
  if (levelled.length === 0 && changes.length === 0) return null;

  const undoLast = () =>
    undo.mutate(routine.id, {
      onSuccess: () => toast.success('Last change undone'),
      onError: () => toast.error(failureMessage('Undo')),
    });

  return (
    <section aria-labelledby="progress-heading" className="space-y-3 rounded-lg border border-border bg-card p-4 text-left" data-testid="routine-progress">
      <h2 id="progress-heading" className="text-lg font-semibold text-foreground">
        Progress
      </h2>
      <ul className="space-y-3">
        {levelled.map((drill) => (
          <li key={drill.name} className="space-y-2">
            <p className="font-medium text-foreground">{drill.name}</p>
            <p className="text-sm text-muted-foreground">{prescription(drill)}</p>
            <LevelSuggestionPrompt routineId={routine.id} drill={drill} />
          </li>
        ))}
      </ul>
      {changes.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-base font-semibold text-foreground">Recent changes</h3>
          <ol className="space-y-1">
            {changes.map((c) => (
              <li key={`${c.changeId}-${c.drillName}`} className="text-sm text-muted-foreground">
                <span className={c.undone ? 'line-through' : 'text-foreground'}>{c.drillName}</span>: {c.reason}
                {c.undone ? ' (undone)' : ''}
              </li>
            ))}
          </ol>
        </div>
      )}
      {routine.canUndo && (
        <Button type="button" variant="outline" className="h-11 w-full" disabled={undo.isPending} onClick={undoLast} data-testid="undo-progression">
          Undo last change
        </Button>
      )}
    </section>
  );
}
