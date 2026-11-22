/**
 * "Set your training max" for an A2S2 lift created without one: either a recent set (weight × reps,
 * the server estimates the TM) or the TM itself. Used by gym mode and Today.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { useSetTrainingMax } from '@/hooks/useWorkouts';
import type { SetTrainingMaxResult } from '@/api/workouts';

const SHEET = 'bottom-0 top-auto max-h-[92dvh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2';

export interface SetTrainingMaxDialogProps {
  open: boolean;
  workoutId: string;
  exerciseId: string;
  exerciseName: string;
  onClose: () => void;
  onSaved?: (result: SetTrainingMaxResult) => void;
}

type Mode = 'recent' | 'direct';

function positive(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(n) && n > 0 ? n : null;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not save the training max. Try again.';
}

export function SetTrainingMaxDialog({ open, workoutId, exerciseId, exerciseName, onClose, onSaved }: SetTrainingMaxDialogProps) {
  const [mode, setMode] = useState<Mode>('recent');
  const [weight, setWeight] = useState('');
  const [reps, setReps] = useState('');
  const [tm, setTm] = useState('');
  const [invalid, setInvalid] = useState(false);
  const save = useSetTrainingMax();

  const submit = () => {
    const w = positive(weight);
    const r = positive(reps);
    const t = positive(tm);
    const request =
      mode === 'recent'
        ? w !== null && r !== null && r <= 30
          ? { recentWeight: w, recentReps: Math.round(r), unit: 'Kilograms' as const }
          : null
        : t !== null
          ? { trainingMax: t, unit: 'Kilograms' as const }
          : null;
    setInvalid(request === null);
    if (!request) return;
    save.mutate(
      { workoutId, exerciseId, request },
      {
        onSuccess: (result) => {
          onSaved?.(result);
          onClose();
        },
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className={SHEET}>
        <form
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <DialogHeader>
            <DialogTitle>Set your training max</DialogTitle>
            <DialogDescription>
              {exerciseName} needs a training max before its first session. Give a recent set and we work it out, or enter it.
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-2" role="group" aria-label="How to set it">
            {(
              [
                ['recent', 'From a recent set'],
                ['direct', 'Enter TM'],
              ] as const
            ).map(([value, text]) => (
              <button
                key={value}
                type="button"
                aria-pressed={mode === value}
                className={cn(
                  'min-h-12 rounded-md border px-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  mode === value ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-muted/40 text-foreground'
                )}
                onClick={() => {
                  setMode(value);
                  setInvalid(false);
                }}
              >
                {text}
              </button>
            ))}
          </div>

          {mode === 'recent' ? (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="tm-recent-weight">Weight (kg)</Label>
                <Input id="tm-recent-weight" inputMode="decimal" className="mt-1 h-12" value={weight} onChange={(e) => setWeight(e.target.value)} />
              </div>
              <div>
                <Label htmlFor="tm-recent-reps">Reps</Label>
                <Input id="tm-recent-reps" inputMode="numeric" className="mt-1 h-12" value={reps} onChange={(e) => setReps(e.target.value)} />
              </div>
              <p className="col-span-2 text-sm text-muted-foreground">A hard set from the last few weeks. The TM is 90 % of the estimated one-rep max.</p>
            </div>
          ) : (
            <div>
              <Label htmlFor="tm-direct">Training max (kg)</Label>
              <Input id="tm-direct" inputMode="decimal" className="mt-1 h-12" value={tm} onChange={(e) => setTm(e.target.value)} />
            </div>
          )}

          {invalid && (
            <p role="alert" className="text-sm text-destructive">
              {mode === 'recent' ? 'Enter the weight and reps (1–30) of a recent set.' : 'Enter a training max in kg.'}
            </p>
          )}
          {save.isError && (
            <p role="alert" className="text-sm text-destructive">
              {serverMessage(save.error)}
            </p>
          )}

          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-12" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" className="h-12" disabled={save.isPending} data-testid="tm-save">
              {save.isPending ? 'Saving…' : 'Save training max'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
