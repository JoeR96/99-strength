/**
 * One lift in the gym checklist: its planned sets pre-filled. Tap a set to confirm it as planned
 * (tap again to undo); the pencil opens weight and reps for a set that went differently. The AMRAP
 * set asks how many reps were done instead of confirming straight away. Every target is ≥ 44 px.
 */

import { useState } from 'react';
import { Check, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import type { GymLift, GymSet } from './gymSession';

export interface GymLiftCardProps {
  lift: GymLift;
  index: number;
  onToggleSet: (lift: number, set: number) => void;
  onAmrap: (lift: number, set: number) => void;
  onEditSet: (lift: number, set: number, change: { weight?: number; reps?: number }) => void;
}

function formatWeight(weight: number, unit: string): string {
  return `${Number.isInteger(weight) ? weight : weight.toFixed(1)} ${unit}`;
}

function setSummary(set: GymSet, unit: string): string {
  const reps = set.isAmrap && !set.done ? `${set.plannedReps}+` : String(set.reps);
  return `${formatWeight(set.weight, unit)} × ${reps}`;
}

export function GymLiftCard({ lift, index, onToggleSet, onAmrap, onEditSet }: GymLiftCardProps) {
  const [editing, setEditing] = useState<number | null>(null);
  const done = lift.sets.filter((s) => s.done).length;
  const headingId = `lift-${index}-heading`;

  return (
    <section className="rounded-lg border border-border bg-card p-4" aria-labelledby={headingId} data-testid={`gym-lift-${index}`}>
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h3 id={headingId} className="min-w-0 truncate text-lg font-semibold text-foreground">
          {lift.name}
        </h3>
        <span className="shrink-0 text-sm text-muted-foreground">
          {done}/{lift.sets.length} sets
        </span>
      </div>

      <ol className="space-y-2">
        {lift.sets.map((set, si) => {
          return (
            <li key={set.setNumber} className="space-y-2">
              <div className="flex items-stretch gap-2">
                <button
                  type="button"
                  className={cn(
                    'flex min-h-14 min-w-0 flex-1 items-center gap-3 rounded-md border px-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                    set.done ? 'border-success/60 bg-success/15 text-foreground' : 'border-border bg-muted/40 text-foreground'
                  )}
                  aria-pressed={set.done}
                  onClick={() => (set.isAmrap && !set.done ? onAmrap(index, si) : onToggleSet(index, si))}
                  data-testid={`set-${index}-${si}`}
                >
                  <span
                    className={cn(
                      'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2',
                      set.done ? 'border-success bg-success text-success-foreground' : 'border-muted-foreground/40'
                    )}
                    aria-hidden="true"
                  >
                    {set.done && <Check className="h-5 w-5" />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm text-muted-foreground">
                      Set {set.setNumber}
                      {set.isAmrap && ' · AMRAP'}
                      {set.isAmrap && <span className="sr-only"> (as many reps as you can)</span>}
                    </span>
                    <span className="block truncate text-lg font-semibold">{setSummary(set, lift.unit)}</span>
                  </span>
                </button>
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  className="h-14 w-12 shrink-0"
                  aria-label={`Change weight or reps for set ${set.setNumber} of ${lift.name}`}
                  aria-expanded={editing === si}
                  onClick={() => setEditing(editing === si ? null : si)}
                >
                  <Pencil aria-hidden="true" />
                </Button>
              </div>

              {editing === si && (
                <div className="grid grid-cols-2 gap-2 rounded-md border border-border p-3">
                  <div>
                    <Label htmlFor={`w-${index}-${si}`}>Weight ({lift.unit})</Label>
                    <Input
                      id={`w-${index}-${si}`}
                      type="number"
                      inputMode="decimal"
                      step="0.5"
                      min={0}
                      className="mt-1 h-12"
                      value={set.weight}
                      onChange={(e) => onEditSet(index, si, { weight: Number(e.target.value) })}
                    />
                  </div>
                  <div>
                    <Label htmlFor={`r-${index}-${si}`}>Reps</Label>
                    <Input
                      id={`r-${index}-${si}`}
                      type="number"
                      inputMode="numeric"
                      min={0}
                      className="mt-1 h-12"
                      value={set.reps}
                      onChange={(e) => onEditSet(index, si, { reps: Number(e.target.value) })}
                    />
                  </div>
                  <Button type="button" className="col-span-2 h-12" onClick={() => setEditing(null)}>
                    Done
                  </Button>
                </div>
              )}
            </li>
          );
        })}
      </ol>
    </section>
  );
}
