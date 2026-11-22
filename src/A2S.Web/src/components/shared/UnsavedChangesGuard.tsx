/**
 * Warns before unsaved changes are left behind: the browser's own prompt on reload or tab close
 * (`beforeunload`), and a "Leave without saving?" dialog on an in-app navigation (react-router's
 * `useBlocker`). The blocker needs a data router; under a plain `<MemoryRouter>` (tests, stories) only
 * the `beforeunload` half runs.
 */

import { useContext, useEffect, useRef } from 'react';
import { UNSAFE_DataRouterContext, useBlocker } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';

export interface UnsavedChangesGuardProps {
  /** Whether leaving now would lose changes; a function is read at the moment of leaving. */
  when: boolean | (() => boolean);
  /** Extra line for the dialog, e.g. that the draft stays on this phone. */
  detail?: string;
}

export function UnsavedChangesGuard({ when, detail }: UnsavedChangesGuardProps) {
  const latest = useRef(when);
  useEffect(() => {
    latest.current = when;
  });
  const shouldBlock = () => (typeof latest.current === 'function' ? latest.current() : latest.current);

  useEffect(() => {
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!(typeof latest.current === 'function' ? latest.current() : latest.current)) return;
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, []);

  const dataRouter = useContext(UNSAFE_DataRouterContext);
  return dataRouter ? <NavigationBlocker shouldBlock={shouldBlock} detail={detail} /> : null;
}

function NavigationBlocker({ shouldBlock, detail }: { shouldBlock: () => boolean; detail?: string }) {
  const blocker = useBlocker(({ currentLocation, nextLocation }) => currentLocation.pathname !== nextLocation.pathname && shouldBlock());
  const blocked = blocker.state === 'blocked';

  return (
    <Dialog open={blocked} onOpenChange={(open) => !open && blocker.reset?.()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Leave without saving?</DialogTitle>
          <DialogDescription>{detail ?? 'Your changes have not been saved.'}</DialogDescription>
        </DialogHeader>
        <DialogFooter className="grid grid-cols-2 gap-2">
          <Button type="button" variant="outline" className="h-11" onClick={() => blocker.reset?.()}>
            Stay
          </Button>
          <Button type="button" className="h-11" onClick={() => blocker.proceed?.()}>
            Leave
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
