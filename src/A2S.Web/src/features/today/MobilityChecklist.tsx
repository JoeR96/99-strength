/**
 * Marks a mobility routine done in one tap, with optional per-drill ticks when the
 * drill names are known (the routines API arrives with Phase 6; until then the
 * list is empty and the sheet is a single confirm).
 */

import { useState } from 'react';
import { Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import type { TodaySessionDto } from '@/types/plan';

export interface MobilityChecklistProps {
  session: TodaySessionDto | null;
  drills?: string[];
  onClose: () => void;
  onSubmit: (session: TodaySessionDto, drillTicks: boolean[] | null) => void;
  saving?: boolean;
}

export function MobilityChecklist({ session, drills = [], onClose, onSubmit, saving = false }: MobilityChecklistProps) {
  const [ticks, setTicks] = useState<boolean[]>(() => drills.map(() => true));

  const toggle = (index: number) => setTicks((prev) => prev.map((t, i) => (i === index ? !t : t)));

  return (
    <Dialog open={session !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="bottom-0 top-auto translate-y-0 rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        <DialogHeader>
          <DialogTitle>{session?.label ?? 'Mobility'}</DialogTitle>
          <DialogDescription>
            {drills.length > 0 ? 'Untick anything you skipped, then mark it done.' : 'Mark the whole routine done.'}
          </DialogDescription>
        </DialogHeader>

        {drills.length > 0 && (
          <ul className="space-y-2">
            {drills.map((drill, index) => (
              <li key={drill}>
                <button
                  type="button"
                  role="checkbox"
                  aria-checked={ticks[index]}
                  onClick={() => toggle(index)}
                  className={cn(
                    'flex min-h-11 w-full items-center gap-3 rounded-md border px-3 py-2 text-left text-sm',
                    ticks[index] ? 'border-success/60 bg-success/10 text-foreground' : 'border-border text-muted-foreground'
                  )}
                >
                  <span
                    className={cn(
                      'flex h-6 w-6 shrink-0 items-center justify-center rounded-full border',
                      ticks[index] ? 'border-success bg-success text-success-foreground' : 'border-border text-transparent'
                    )}
                    aria-hidden="true"
                  >
                    <Check className="h-4 w-4" strokeWidth={3} />
                  </span>
                  {drill}
                </button>
              </li>
            ))}
          </ul>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button type="button" variant="outline" onClick={onClose} className="h-12">
            Cancel
          </Button>
          <Button
            type="button"
            className="h-12"
            disabled={saving || !session}
            onClick={() => session && onSubmit(session, drills.length > 0 ? ticks : null)}
          >
            {saving ? 'Saving…' : 'Mark done'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
