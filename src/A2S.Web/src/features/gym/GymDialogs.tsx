/** The AMRAP reps prompt and the Finish early confirmation for gym mode. */

import { useState } from 'react';
import { Minus, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

const SHEET = 'bottom-0 top-auto max-h-[92dvh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2';

export interface AmrapPromptProps {
  open: boolean;
  exerciseName: string;
  weightLabel: string;
  targetReps: number;
  onSubmit: (reps: number) => void;
  onCancel: () => void;
}

export function AmrapPrompt({ open, exerciseName, weightLabel, targetReps, onSubmit, onCancel }: AmrapPromptProps) {
  const [reps, setReps] = useState(targetReps);
  const clamp = (value: number) => Math.min(Math.max(0, Math.round(value) || 0), 200);

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onCancel()}>
      <DialogContent className={SHEET}>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSubmit(clamp(reps));
          }}
        >
          <DialogHeader>
            <DialogTitle>How many reps?</DialogTitle>
            <DialogDescription>
              {exerciseName}, last set at {weightLabel}. The target was {targetReps}; beating it raises your training max.
            </DialogDescription>
          </DialogHeader>
          <div className="flex items-end gap-2">
            <Button type="button" variant="outline" size="icon" className="h-14 w-14 shrink-0" aria-label="One rep fewer" onClick={() => setReps((r) => clamp(r - 1))}>
              <Minus aria-hidden="true" />
            </Button>
            <div className="min-w-0 flex-1">
              <Label htmlFor="amrap-reps">Reps done</Label>
              <Input
                id="amrap-reps"
                type="number"
                inputMode="numeric"
                min={0}
                className="mt-1 h-14 text-center text-2xl font-semibold"
                value={reps}
                onChange={(e) => setReps(clamp(Number(e.target.value)))}
                autoFocus
              />
            </div>
            <Button type="button" variant="outline" size="icon" className="h-14 w-14 shrink-0" aria-label="One rep more" onClick={() => setReps((r) => clamp(r + 1))}>
              <Plus aria-hidden="true" />
            </Button>
          </div>
          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-12" onClick={onCancel}>
              Cancel
            </Button>
            <Button type="submit" className="h-12" data-testid="amrap-save">
              Save {reps} reps
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export interface FinishEarlyDialogProps {
  open: boolean;
  /** Lifts with nothing confirmed: left out, progression untouched. */
  skipped: string[];
  setsLeft: number;
  onConfirm: () => void;
  onCancel: () => void;
}

export function FinishEarlyDialog({ open, skipped, setsLeft, onConfirm, onCancel }: FinishEarlyDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onCancel()}>
      <DialogContent className={SHEET}>
        <DialogHeader>
          <DialogTitle>Finish early?</DialogTitle>
          <DialogDescription>
            Only what you ticked is saved and sent to Hevy. {setsLeft} {setsLeft === 1 ? 'set is' : 'sets are'} left unticked.
          </DialogDescription>
        </DialogHeader>
        {skipped.length > 0 && (
          <div className="text-sm text-foreground">
            <p className="font-medium">Left out (progression waits for next time):</p>
            <ul className="mt-1 list-disc pl-5 text-muted-foreground">
              {skipped.map((name) => (
                <li key={name}>{name}</li>
              ))}
            </ul>
          </div>
        )}
        <DialogFooter className="gap-2">
          <Button type="button" variant="outline" className="h-12" onClick={onCancel}>
            Keep going
          </Button>
          <Button type="button" className="h-12" onClick={onConfirm} data-testid="confirm-finish-early">
            Finish early
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
