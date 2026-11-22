/**
 * After a home routine: one row per drill with levels, "Felt clean" by default, one tap to say "Strained".
 * Saving runs the light progression on the server (offline it queues) and then shows what changes next time,
 * e.g. "Next time: Level 2: Hands on a bench, straight legs".
 */

import { useState } from 'react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useRecordDrillFeedback } from '@/hooks/useMobilityRoutines';
import { DrillOutcome, type MobilityRoutineDto } from '@/types/mobility';
import { defaultFeedback, drillsToRate, nextTimeLines, prescription } from './drillLevels';
import { LevelSuggestionPrompt } from './RoutineProgress';

interface Props {
  routine: MobilityRoutineDto;
  ticks: boolean[];
  /** Made when the player opened, so a replayed save counts once. */
  sessionKey: string;
}

export function DrillFeedbackPanel({ routine, ticks, sessionKey }: Props) {
  const rate = drillsToRate(routine, ticks);
  const [strained, setStrained] = useState<Set<string>>(new Set());
  const [result, setResult] = useState<{ lines: string[]; queued: boolean } | null>(null);
  const record = useRecordDrillFeedback();

  if (rate.length === 0 && !result) return null;

  const toggle = (name: string) =>
    setStrained((s) => {
      const next = new Set(s);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });

  const save = () => {
    const feedback = defaultFeedback(routine, ticks).map((f) => (strained.has(f.drillName) ? { ...f, outcome: DrillOutcome.Strained } : f));
    record.mutate(
      { id: routine.id, request: { sessionKey, feedback }, label: routine.name },
      {
        onSuccess: (outcome) =>
          setResult(outcome.queued ? { lines: [], queued: true } : { lines: nextTimeLines(outcome.data.routine, outcome.data.changes), queued: false }),
        onError: () => toast.error("Couldn't save how it felt. Try again."),
      }
    );
  };

  if (result) {
    return (
      <section aria-labelledby="next-time-heading" className="space-y-2 rounded-lg border border-border bg-card p-4 text-left" data-testid="next-time">
        <h2 id="next-time-heading" className="text-lg font-semibold text-foreground">
          {result.queued ? 'Saved on this phone' : 'Next time'}
        </h2>
        {result.queued && <p className="text-sm text-muted-foreground">It will sync when you're back online.</p>}
        {!result.queued && result.lines.length === 0 && <p className="text-sm text-muted-foreground">Same again. Two clean sessions in a row move you on.</p>}
        {result.lines.map((line) => (
          <p key={line} className="text-base font-medium text-foreground">
            {line}
          </p>
        ))}
        {/* `routine` is the live cached routine, so an answered suggestion disappears. */}
        {routine.drills.map((d) => (
          <LevelSuggestionPrompt key={d.name} routineId={routine.id} drill={d} />
        ))}
      </section>
    );
  }

  return (
    <section aria-labelledby="felt-heading" className="space-y-3 rounded-lg border border-border bg-card p-4 text-left" data-testid="drill-feedback">
      <h2 id="felt-heading" className="text-lg font-semibold text-foreground">
        How did it feel?
      </h2>
      <p className="text-sm text-muted-foreground">Clean means the cues held without strain.</p>
      <ul className="space-y-2">
        {rate.map((drill) => {
          const isStrained = strained.has(drill.name);
          return (
            <li key={drill.name} className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="font-medium text-foreground">{drill.name}</p>
                <p className="text-sm text-muted-foreground">{prescription(drill)}</p>
              </div>
              <Button
                type="button"
                variant="outline"
                className={cn('h-11 shrink-0', isStrained ? 'border-destructive/60 text-destructive' : 'border-success/60 text-success')}
                aria-pressed={isStrained}
                aria-label={`${drill.name}: ${isStrained ? 'strained, tap for clean' : 'felt clean, tap if it strained'}`}
                onClick={() => toggle(drill.name)}
              >
                {isStrained ? 'Strained' : 'Felt clean'}
              </Button>
            </li>
          );
        })}
      </ul>
      <Button type="button" className="h-12 w-full" onClick={save} disabled={record.isPending} data-testid="save-feedback">
        {record.isPending ? 'Saving…' : 'Save how it felt'}
      </Button>
    </section>
  );
}
