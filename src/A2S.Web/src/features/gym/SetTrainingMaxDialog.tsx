/**
 * "Set your training max" for an A2S2 lift created without one: either a recent set (weight × reps,
 * the server estimates the TM) or the TM itself. Used by gym mode and Today. When Hevy history gives a
 * suggestion it is prefilled as the TM with its evidence; editing the field overrides it.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { useSetTrainingMax, useStartingNumberSuggestions } from '@/hooks/useWorkouts';
import type { SetTrainingMaxResult, StartingNumberSuggestion } from '@/api/workouts';
import { hevyFallbackNote, itemFor } from '@/features/suggestions/startingNumbers';
import { SuggestionEvidence } from '@/features/suggestions/SuggestionEvidence';

const SHEET = 'sheet-surface bottom-0 top-auto w-full translate-y-0 overflow-y-auto rounded-b-none rounded-t-2xl p-6 pb-[calc(1.5rem+env(safe-area-inset-bottom))] sm:bottom-auto sm:top-1/2 sm:w-[calc(100%-2rem)] sm:-translate-y-1/2 sm:rounded-2xl sm:pb-6 max-h-[92dvh]';

export interface SetTrainingMaxDialogProps {
  open: boolean;
  workoutId: string;
  exerciseId: string;
  exerciseName: string;
  onClose: () => void;
  onSaved?: (result: SetTrainingMaxResult) => void;
  /** Given (stories, callers that already loaded it): skip the fetch. Null = no suggestion. */
  suggestion?: StartingNumberSuggestion | null;
  /** Given with `suggestion`: why there is none (Hevy unavailable / not connected). */
  fallbackNote?: string | null;
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

export function SetTrainingMaxDialog({
  open,
  workoutId,
  exerciseId,
  exerciseName,
  onClose,
  onSaved,
  suggestion: givenSuggestion,
  fallbackNote: givenNote,
}: SetTrainingMaxDialogProps) {
  const given = givenSuggestion !== undefined;
  const query = useStartingNumberSuggestions(workoutId, open && !given);
  const suggestion = given ? givenSuggestion : (itemFor(query.data, exerciseId)?.suggestion ?? null);
  const note = given ? (givenNote ?? null) : hevyFallbackNote(query.data, query.isError);

  // Untouched fields follow the suggestion; anything typed wins.
  const [chosenMode, setMode] = useState<Mode | null>(null);
  const [weight, setWeight] = useState('');
  const [reps, setReps] = useState('');
  const [typedTm, setTm] = useState<string | null>(null);
  const mode: Mode = chosenMode ?? (suggestion ? 'direct' : 'recent');
  const tm = typedTm ?? (suggestion ? String(suggestion.valueKg) : '');
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

          {suggestion ? (
            <SuggestionEvidence
              suggestion={suggestion}
              onUse={() => {
                setMode('direct');
                setTm(String(suggestion.valueKg));
                setInvalid(false);
              }}
            />
          ) : (
            note && (
              <p className="text-sm text-muted-foreground" data-testid="suggestion-fallback">
                {note}
              </p>
            )
          )}

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
                  mode === value ? 'border-primary bg-primary text-primary-foreground' : 'border-transparent bg-foreground/[0.06] text-foreground'
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
