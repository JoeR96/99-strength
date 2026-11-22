import { Dumbbell, StretchHorizontal, CheckCircle2, TrendingUp, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export interface TabDefinition {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Route prefixes that light this tab up. */
  matches: string[];
  /** Route prefixes under `matches` that belong to another tab (the Hevy inbox is Today's, not Settings'). */
  excludes?: string[];
  /** A live count shown on the tab; `pendingSync` is the offline queue length. */
  badge?: 'pendingSync';
}

export const TABS: TabDefinition[] = [
  // Workout = the current session in the cycle plus the plan's week view (the old Plan tab).
  {
    to: '/today',
    label: 'Workout',
    icon: Dumbbell,
    matches: ['/today', '/current', '/dashboard', '/hevy/inbox', '/gym', '/workout', '/setup', '/programs', '/plans'],
    badge: 'pendingSync',
  },
  { to: '/routines', label: 'Mobility', icon: StretchHorizontal, matches: ['/routines'] },
  { to: '/habits', label: 'Habits', icon: CheckCircle2, matches: ['/habits'], badge: 'pendingSync' },
  { to: '/progress', label: 'Progress', icon: TrendingUp, matches: ['/progress', '/history', '/hevy/data', '/cardio', '/cardio-tracks'] },
  {
    to: '/settings',
    label: 'Settings',
    icon: Settings,
    matches: ['/settings', '/hevy', '/exercises', '/simulate'],
    excludes: ['/hevy/inbox', '/hevy/data'],
  },
];

function underPrefix(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

export function isTabActive(tab: TabDefinition, pathname: string): boolean {
  if (tab.excludes?.some((m) => underPrefix(pathname, m))) return false;
  return tab.matches.some((m) => underPrefix(pathname, m));
}
