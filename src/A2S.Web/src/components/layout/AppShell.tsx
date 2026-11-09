/**
 * Wraps every signed-in page: reserves room for the bottom tab bar on phones
 * (so content never hides behind it) and renders the bar itself.
 */

import type { ReactNode } from 'react';
import { BottomTabBar } from './BottomTabBar';

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <>
      <div className="pb-[calc(3.5rem+env(safe-area-inset-bottom))] lg:pb-0">{children}</div>
      <BottomTabBar />
    </>
  );
}
