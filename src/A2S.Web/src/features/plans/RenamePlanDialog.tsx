/**
 * Rename the plan: one name field, Save / Cancel. The name is trimmed and must be 1–200 characters;
 * the server says the same. A toast confirms the new name.
 */

import { useEffect, useId, useState } from 'react';
import { Pencil } from 'lucide-react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useRenamePlan } from '@/hooks/usePlan';

export const MAX_PLAN_NAME_LENGTH = 200;

/** Null when the name is fine, else what is wrong with it. */
export function planNameError(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed === '') return 'Give the plan a name.';
  if (trimmed.length > MAX_PLAN_NAME_LENGTH) return `Keep it to ${MAX_PLAN_NAME_LENGTH} characters or fewer.`;
  return null;
}

function serverMessage(error: unknown): string {
  const data = (error as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return data?.detail ?? data?.title ?? 'Could not rename the plan. Try again.';
}

export interface RenamePlanDialogProps {
  open: boolean;
  planId: string;
  currentName: string;
  onClose: () => void;
}

export function RenamePlanDialog({ open, planId, currentName, onClose }: RenamePlanDialogProps) {
  const id = useId();
  const [name, setName] = useState(currentName);
  const [error, setError] = useState<string | null>(null);
  const rename = useRenamePlan();

  useEffect(() => {
    if (open) {
      setName(currentName);
      setError(null);
      rename.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens
  }, [open, currentName]);

  const submit = () => {
    const problem = planNameError(name);
    setError(problem);
    if (problem) return;
    const trimmed = name.trim();
    rename.mutate(
      { planId, name: trimmed },
      {
        onSuccess: (plan) => {
          toast.success(`Plan renamed to ${plan.name}`);
          onClose();
        },
      }
    );
  };

  const message = error ?? (rename.isError ? serverMessage(rename.error) : null);

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent>
        <form
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <DialogHeader>
            <DialogTitle>Rename plan</DialogTitle>
            <DialogDescription>The new name shows across the app and on routines sent to Hevy from now on.</DialogDescription>
          </DialogHeader>
          <div>
            <Label htmlFor={`${id}-name`}>Plan name</Label>
            <Input
              id={`${id}-name`}
              className="mt-1 h-11"
              value={name}
              maxLength={MAX_PLAN_NAME_LENGTH + 50}
              autoComplete="off"
              aria-invalid={message ? true : undefined}
              aria-describedby={message ? `${id}-error` : undefined}
              onChange={(e) => {
                setName(e.target.value);
                if (error) setError(null);
              }}
            />
            {message && (
              <p id={`${id}-error`} role="alert" className="mt-1 text-sm text-destructive">
                {message}
              </p>
            )}
          </div>
          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" className="h-11" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" className="h-11" disabled={rename.isPending}>
              {rename.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** The plan's name with a pencil button that opens {@link RenamePlanDialog}. */
export function PlanNameWithRename({ planId, name, className }: { planId: string; name: string; className?: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div className={className ? `flex items-center gap-1 ${className}` : 'flex items-center gap-1'}>
      <p className="min-w-0 truncate text-sm text-muted-foreground">{name}</p>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="h-11 w-11 shrink-0"
        aria-label="Rename plan"
        onClick={() => setOpen(true)}
      >
        <Pencil aria-hidden="true" />
      </Button>
      <RenamePlanDialog open={open} planId={planId} currentName={name} onClose={() => setOpen(false)} />
    </div>
  );
}
