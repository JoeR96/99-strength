/**
 * One block of the gym checklist: a lift on its own, or a superset (2a/2b) whose sets alternate
 * A1 → B1 → A2 → B2 …. Tap a set to confirm it as planned (tap again to undo); the pencil opens weight
 * and reps for a set that went differently. The AMRAP set asks how many reps were done instead of
 * confirming straight away. A lift with an extra set shows "+1 left" after its sets. A lift without a
 * training max shows reps and intensity (no kg) and a "Set your training max" prompt. Targets ≥ 44 px.
 */

import { useState } from 'react';
import { Check, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { sideRows, type GymLift, type GymSet, type LiftBlock } from './gymSession';

export interface GymBlockCardProps {
  block: LiftBlock;
  lifts: GymLift[];
  onToggleSet: (lift: number, set: number) => void;
  onAmrap: (lift: number, set: number) => void;
  onEditSet: (lift: number, set: number, change: { weight?: number; reps?: number }) => void;
  onToggleExtraSet?: (lift: number, change?: { weight?: number; reps?: number }) => void;
  onSetTrainingMax?: (lift: number) => void;
  /** Unilateral lifts: ticks the left side of a set (done first). */
  onToggleLeft?: (lift: number, set: number) => void;
}

function formatWeight(weight: number, unit: string): string {
  return `${Number.isInteger(weight) ? weight : weight.toFixed(1)} ${unit}`;
}

/** "5 × 5 @ 70 %" for a lift without a training max. */
export function pendingSummary(lift: GymLift): string {
  const reps = lift.sets[0]?.plannedReps ?? 0;
  const pct = lift.intensity ? ` @ ${Math.round(lift.intensity * 100)} %` : '';
  return `${lift.sets.length} × ${reps}${pct}`;
}

function setSummary(set: GymSet, lift: GymLift): string {
  const reps = set.isAmrap && !set.done ? `${set.plannedReps}+` : String(set.reps);
  if (lift.trainingMaxPending && set.weight === 0) {
    return `${reps} reps${lift.intensity ? ` @ ${Math.round(lift.intensity * 100)} %` : ''}`;
  }
  return `${formatWeight(set.weight, lift.unit)} × ${reps}`;
}

function TickCircle({ done }: { done: boolean }) {
  return (
    <span
      className={cn(
        'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2',
        done ? 'border-success bg-success text-success-foreground' : 'border-muted-foreground/40'
      )}
      aria-hidden="true"
    >
      {done && <Check className="h-5 w-5" />}
    </span>
  );
}

interface SetRowProps {
  id: string;
  caption: React.ReactNode;
  summary: string;
  done: boolean;
  unit: string;
  weight: number;
  reps: number;
  editLabel: string;
  testId: string;
  onTap: () => void;
  onEdit: (change: { weight?: number; reps?: number }) => void;
}

function SetRow({ id, caption, summary, done, unit, weight, reps, editLabel, testId, onTap, onEdit }: SetRowProps) {
  const [editing, setEditing] = useState(false);
  return (
    <li className="space-y-2">
      <div className="flex items-stretch gap-2">
        <button
          type="button"
          className={cn(
            'flex min-h-14 min-w-0 flex-1 items-center gap-3 rounded-md border px-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            done ? 'border-success/60 bg-success/15 text-foreground' : 'border-border bg-muted/40 text-foreground'
          )}
          aria-pressed={done}
          onClick={onTap}
          data-testid={testId}
        >
          <TickCircle done={done} />
          <span className="min-w-0 flex-1">
            <span className="block text-sm text-muted-foreground">{caption}</span>
            <span className="block truncate text-lg font-semibold">{summary}</span>
          </span>
        </button>
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="h-14 w-12 shrink-0"
          aria-label={editLabel}
          aria-expanded={editing}
          onClick={() => setEditing(!editing)}
        >
          <Pencil aria-hidden="true" />
        </Button>
      </div>

      {editing && (
        <div className="grid grid-cols-2 gap-2 rounded-md border border-border p-3">
          <div>
            <Label htmlFor={`w-${id}`}>Weight ({unit})</Label>
            <Input id={`w-${id}`} type="number" inputMode="decimal" step="0.5" min={0} className="mt-1 h-12" value={weight} onChange={(e) => onEdit({ weight: Number(e.target.value) })} />
          </div>
          <div>
            <Label htmlFor={`r-${id}`}>Reps</Label>
            <Input id={`r-${id}`} type="number" inputMode="numeric" min={0} className="mt-1 h-12" value={reps} onChange={(e) => onEdit({ reps: Number(e.target.value) })} />
          </div>
          <Button type="button" className="col-span-2 h-12" onClick={() => setEditing(false)}>
            Done
          </Button>
        </div>
      )}
    </li>
  );
}

export function GymBlockCard({ block, lifts, onToggleSet, onAmrap, onEditSet, onToggleExtraSet, onSetTrainingMax, onToggleLeft }: GymBlockCardProps) {
  const first = block.lifts[0];
  const superset = block.lifts.length > 1;
  const headingId = `lift-${first}-heading`;
  const labelOf = (lift: number) => block.labels[block.lifts.indexOf(lift)];
  const total = block.lifts.reduce((n, l) => n + lifts[l].sets.length, 0);
  const done = block.lifts.reduce((n, l) => n + lifts[l].sets.filter((s) => s.done).length, 0);

  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby={headingId} data-testid={superset ? `gym-superset-${first}` : `gym-lift-${first}`}>
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h3 id={headingId} className="min-w-0 text-lg font-semibold text-foreground">
          {superset ? (
            <>
              <span className="block text-sm font-medium text-muted-foreground">Superset · no rest between</span>
              {block.lifts.map((l) => (
                <span key={l} className="block truncate">
                  {labelOf(l)} {lifts[l].name}
                </span>
              ))}
            </>
          ) : (
            <span className="block truncate">{lifts[first].name}</span>
          )}
        </h3>
        <span className="shrink-0 text-sm text-muted-foreground">
          {done}/{total} sets
        </span>
      </div>

      {block.lifts
        .filter((l) => lifts[l].trainingMaxPending)
        .map((l) => (
          <div key={l} className="mb-3 rounded-md border border-warning/50 bg-warning/10 p-3" data-testid={`tm-pending-${l}`}>
            <p className="text-sm text-foreground">
              No training max for {lifts[l].name} yet: {pendingSummary(lifts[l])}. Set it to see the weights.
            </p>
            {onSetTrainingMax && (
              <Button type="button" variant="outline" className="mt-2 h-11 w-full" onClick={() => onSetTrainingMax(l)}>
                Set your training max
              </Button>
            )}
          </div>
        ))}

      <ol className="space-y-2">
        {block.order.flatMap(({ lift: li, set: si }) => {
          const lift = lifts[li];
          const set = lift.sets[si];
          const label = labelOf(li);
          return sideRows(lift).map((side) => side === 'Left' ? (
            <SetRow
              key={`${li}-${set.setNumber}-left`}
              id={`${li}-${si}-left`}
              caption={`${label ? `${label} · ` : ''}Set ${set.setNumber} Left`}
              summary={setSummary(set, lift)}
              done={set.leftDone === true}
              unit={lift.unit}
              weight={set.weight}
              reps={set.reps}
              editLabel={`Change weight or reps for set ${set.setNumber} left of ${lift.name}`}
              testId={`set-${li}-${si}-left`}
              onTap={() => onToggleLeft?.(li, si)}
              onEdit={(change) => onEditSet(li, si, change)}
            />
          ) : (
            <SetRow
              key={`${li}-${set.setNumber}`}
              id={`${li}-${si}`}
              caption={
                <>
                  {label ? `${label} · ` : ''}Set {set.setNumber}{side ? ' Right' : ''}
                  {set.isAmrap && ' · AMRAP'}
                  {set.isAmrap && <span className="sr-only"> (as many reps as you can)</span>}
                </>
              }
              summary={setSummary(set, lift)}
              done={set.done}
              unit={lift.unit}
              weight={set.weight}
              reps={set.reps}
              editLabel={`Change weight or reps for set ${set.setNumber} of ${lift.name}`}
              testId={`set-${li}-${si}`}
              onTap={() => (set.isAmrap && !set.done ? onAmrap(li, si) : onToggleSet(li, si))}
              onEdit={(change) => onEditSet(li, si, change)}
            />
          ));
        })}
        {block.lifts
          .filter((l) => lifts[l].extraSet)
          .map((l) => {
            const lift = lifts[l];
            const extra = lift.extraSet!;
            const side = extra.side.toLowerCase();
            return (
              <SetRow
                key={`${l}-extra`}
                id={`${l}-extra`}
                caption={`${labelOf(l) ? `${labelOf(l)} · ` : ''}Extra set · ${lift.name}`}
                summary={`+1 ${side} · ${formatWeight(extra.weight, lift.unit)} × ${extra.reps}`}
                done={extra.done}
                unit={lift.unit}
                weight={extra.weight}
                reps={extra.reps}
                editLabel={`Change weight or reps for the extra ${side} set of ${lift.name}`}
                testId={`extra-set-${l}`}
                onTap={() => onToggleExtraSet?.(l)}
                onEdit={(change) => onToggleExtraSet?.(l, change)}
              />
            );
          })}
      </ol>
    </section>
  );
}
