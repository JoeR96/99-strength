/** Renders a habit's icon from its stored key (falls back by type). */

import { createElement } from 'react';
import type { HabitType } from '@/types/habits';
import { habitIcon } from './habitIcons';

export interface HabitIconProps {
  icon: string | null | undefined;
  type: HabitType;
  className?: string;
}

export function HabitIcon({ icon, type, className }: HabitIconProps) {
  return createElement(habitIcon(icon, type), { className, 'aria-hidden': true });
}
