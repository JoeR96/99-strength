/**
 * Places an inbox workout in one tap: pick one of that date's sessions to complete it from the
 * workout, or record the workout as an extra session that leaves A2S2 progression alone.
 */

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { formatDayLabel } from '@/lib/localDate';
import { useActivePlan, useToday } from '@/hooks/usePlan';
import { SessionKind, SessionStatus, sessionKindLabel } from '@/types/plan';
import type { AssignHevyWorkoutRequest, HevyInboxItemDto } from '@/types/hevySync';
import { assignRequestFor, extraKindFor, extraRequestFor } from './inboxAssign';

export interface HevyAssignSheetProps {
  /** The workout being placed; null closes the sheet. */
  item: HevyInboxItemDto | null;
  onClose: () => void;
  /** `label` is the session's name for the toast, or null for an extra. */
  onAssign: (item: HevyInboxItemDto, request: AssignHevyWorkoutRequest, label: string | null) => void;
  saving?: boolean;
  /** Why the last attempt failed, shown in the sheet so the choice can be retried. */
  error?: string | null;
}

export function HevyAssignSheet({ item, onClose, onAssign, saving = false, error = null }: HevyAssignSheetProps) {
  return (
    <Dialog open={item !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="bottom-0 top-auto max-h-[85vh] translate-y-0 gap-4 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        <DialogHeader>
          <DialogTitle>{item ? `Assign ${item.title}` : 'Assign workout'}</DialogTitle>
          <DialogDescription>
            {item ? `Trained on ${formatDayLabel(item.localDate)}. Choose what it counts as.` : 'Choose what it counts as.'}
          </DialogDescription>
        </DialogHeader>

        {item && <AssignChoices item={item} onAssign={onAssign} saving={saving} />}

        {error && (
          <p className="text-sm text-destructive" role="alert">
            {error}
          </p>
        )}

        <Button type="button" variant="outline" onClick={onClose} className="h-12 w-full">
          Cancel
        </Button>
      </DialogContent>
    </Dialog>
  );
}

function AssignChoices({
  item,
  onAssign,
  saving,
}: {
  item: HevyInboxItemDto;
  onAssign: HevyAssignSheetProps['onAssign'];
  saving: boolean;
}) {
  const dayQuery = useToday(item.localDate);
  const planQuery = useActivePlan();
  const sessions = (dayQuery.data?.sessions ?? []).filter((s) => s.kind !== SessionKind.Rest);
  const extraKind = sessionKindLabel[extraKindFor(item)].toLowerCase();

  return (
    <div className="space-y-4" data-testid="hevy-assign-choices">
      <section aria-labelledby="assign-sessions-heading" className="space-y-2">
        <h3 id="assign-sessions-heading" className="text-sm font-medium text-muted-foreground">
          Sessions on {formatDayLabel(item.localDate)}
        </h3>

        {dayQuery.isLoading && <div className="h-14 animate-pulse rounded-md bg-muted" aria-hidden="true" />}

        {dayQuery.isError && (
          <p className="text-sm text-muted-foreground" role="status">
            Couldn't load that day's sessions. You can still record it as an extra.
          </p>
        )}

        {dayQuery.isSuccess && sessions.length === 0 && (
          <p className="text-sm text-muted-foreground">
            {dayQuery.data === null ? 'There is no active plan to assign it to.' : 'Nothing was planned that day.'}
          </p>
        )}

        {sessions.length > 0 && (
          <ul className="space-y-2">
            {sessions.map((session, index) => {
              const done = session.status === SessionStatus.Done;
              return (
                <li key={`${session.kind}-${session.label}-${index}`}>
                  <Button
                    type="button"
                    className="h-14 w-full justify-between px-4 text-base"
                    disabled={saving || done}
                    onClick={() => onAssign(item, assignRequestFor(item, session), session.label)}
                    data-testid="assign-to-session"
                  >
                    <span className="truncate">{session.label}</span>
                    <span className="text-sm font-normal">{done ? 'Already done' : sessionKindLabel[session.kind]}</span>
                  </Button>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <section className="space-y-2">
        <Button
          type="button"
          variant="outline"
          className="h-14 w-full justify-between px-4 text-base"
          disabled={saving || dayQuery.data === null}
          onClick={() => onAssign(item, extraRequestFor(item, planQuery.data), null)}
          data-testid="assign-as-extra"
        >
          <span>Record as extra</span>
          <span className="text-sm font-normal text-muted-foreground">Extra {extraKind}</span>
        </Button>
        <p className="text-xs text-muted-foreground">
          An extra counts towards the week and leaves your A2S2 progression as it is.
        </p>
      </section>
    </div>
  );
}
