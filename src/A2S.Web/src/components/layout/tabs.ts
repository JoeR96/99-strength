import { Sun, CalendarDays, CheckCircle2, TrendingUp, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export interface TabDefinition {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Route prefixes that light this tab up. */
  matches: string[];
  /** A live count shown on the tab; `pendingSync` is the offline queue length. */
  badge?: 'pendingSync';
}

export const TABS: TabDefinition[] = [
  { to: '/today', label: 'Today', icon: Sun, matches: ['/today', '/dashboard'], badge: 'pendingSync' },
  { to: '/workout', label: 'Plan', icon: CalendarDays, matches: ['/workout', '/setup', '/programs'] },
  { to: '/habits', label: 'Habits', icon: CheckCircle2, matches: ['/habits'], badge: 'pendingSync' },
  { to: '/progress', label: 'Progress', icon: TrendingUp, matches: ['/progress', '/history', '/hevy/data'] },
  { to: '/settings', label: 'Settings', icon: Settings, matches: ['/settings', '/hevy', '/exercises', '/simulate'] },
];

export function isTabActive(tab: TabDefinition, pathname: string): boolean {
  return tab.matches.some((m) => pathname === m || pathname.startsWith(`${m}/`));
}
