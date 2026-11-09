import { Sun, CalendarDays, CheckCircle2, TrendingUp, Settings } from 'lucide-react';
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
  { to: '/today', label: 'Today', icon: Sun, matches: ['/today', '/dashboard', '/hevy/inbox'], badge: 'pendingSync' },
  { to: '/workout', label: 'Plan', icon: CalendarDays, matches: ['/workout', '/setup', '/programs'] },
  { to: '/habits', label: 'Habits', icon: CheckCircle2, matches: ['/habits'], badge: 'pendingSync' },
  { to: '/progress', label: 'Progress', icon: TrendingUp, matches: ['/progress', '/history', '/hevy/data'] },
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
