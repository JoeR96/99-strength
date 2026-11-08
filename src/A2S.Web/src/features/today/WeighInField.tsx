/**
 * Today's weigh-in. When a value is already logged it shows the number with an
 * Edit button rather than an empty input; saving the same value again is a no-op.
 */

import { useState, type FormEvent } from 'react';
import { Pencil, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { parseKg } from './weighIn';

export interface WeighInFieldProps {
  /** Today's recorded weight, or `null` when none has been logged. */
  valueKg: number | null;
  onSave: (kg: number) => void;
  saving?: boolean;
}

export function WeighInField({ valueKg, onSave, saving = false }: WeighInFieldProps) {
  const [editing, setEditing] = useState(valueKg === null);
  const [draft, setDraft] = useState(valueKg === null ? '' : String(valueKg));
  const [error, setError] = useState<string | null>(null);
  const [seenValue, setSeenValue] = useState(valueKg);

  // A new value from the server (saved, or loaded late) closes the editor and refreshes the draft.
  if (valueKg !== seenValue) {
    setSeenValue(valueKg);
    if (valueKg !== null) {
      setEditing(false);
      setDraft(String(valueKg));
    }
  }

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const kg = parseKg(draft);
    if (kg === null) {
      setError('Enter a weight in kg, like 94.6');
      return;
    }
    setError(null);
    if (valueKg !== null && kg === valueKg) {
      setEditing(false);
      return;
    }
    onSave(kg);
  };

  if (!editing && valueKg !== null) {
    return (
      <div className="flex items-center gap-3 rounded-lg border border-border bg-card p-3" data-testid="weigh-in">
        <Scale className="h-6 w-6 shrink-0 text-muted-foreground" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <div className="text-sm font-medium text-foreground">Morning weigh-in</div>
          <div className="text-2xl font-semibold tabular-nums text-foreground">
            {valueKg.toFixed(1)}
            <span className="text-base font-normal text-muted-foreground"> kg</span>
          </div>
        </div>
        <Button type="button" variant="outline" size="sm" className="h-11" onClick={() => setEditing(true)}>
          <Pencil aria-hidden="true" />
          Edit
        </Button>
      </div>
    );
  }

  return (
    <form
      onSubmit={submit}
      className="flex items-center gap-3 rounded-lg border border-border bg-card p-3"
      data-testid="weigh-in"
    >
      <Scale className="h-6 w-6 shrink-0 text-muted-foreground" aria-hidden="true" />
      <div className="min-w-0 flex-1">
        <label htmlFor="weigh-in-kg" className="text-sm font-medium text-foreground">
          Morning weigh-in
        </label>
        <div className="mt-1 flex items-center gap-2">
          <Input
            id="weigh-in-kg"
            inputMode="decimal"
            autoComplete="off"
            placeholder="94.6"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? 'weigh-in-error' : undefined}
            className="h-11 w-28 text-lg"
          />
          <span className="text-sm text-muted-foreground">kg</span>
        </div>
        {error && (
          <p id="weigh-in-error" className="mt-1 text-xs text-destructive">
            {error}
          </p>
        )}
      </div>
      <div className="flex shrink-0 flex-col gap-2">
        <Button type="submit" size="sm" className="h-11" disabled={saving}>
          {saving ? 'Saving…' : 'Save'}
        </Button>
        {valueKg !== null && (
          <Button type="button" variant="ghost" size="sm" className="h-11" onClick={() => setEditing(false)}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  );
}
