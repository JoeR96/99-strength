import { Sun, CalendarDays, CheckCircle2, TrendingUp, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export interface TabDefinition {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Route prefixes that light this tab up. */
  matches: string[];
}

export const TABS: TabDefinition[] = [
  { to: '/dashboard', label: 'Today', icon: Sun, matches: ['/dashboard', '/today'] },
  { to: '/workout', label: 'Plan', icon: CalendarDays, matches: ['/workout', '/setup', '/programs'] },
  { to: '/habits', label: 'Habits', icon: CheckCircle2, matches: ['/habits'] },
  { to: '/history', label: 'Progress', icon: TrendingUp, matches: ['/history', '/hevy/data'] },
  { to: '/settings', label: 'Settings', icon: Settings, matches: ['/settings', '/hevy', '/exercises', '/simulate'] },
];

export function isTabActive(tab: TabDefinition, pathname: string): boolean {
  return tab.matches.some((m) => pathname === m || pathname.startsWith(`${m}/`));
}
