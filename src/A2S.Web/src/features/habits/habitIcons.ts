/** Maps a habit's stored `icon` key to a lucide icon. Unknown keys fall back by type. */

import {
  Circle,
  CheckCircle2,
  Droplets,
  Footprints,
  Scale,
  Pill,
  StretchHorizontal,
  Dumbbell,
  Moon,
  BookOpen,
  Apple,
  Sun,
  Hash,
  type LucideIcon,
} from 'lucide-react';
import type { HabitType } from '@/types/habits';

export const HABIT_ICONS: Record<string, LucideIcon> = {
  droplets: Droplets,
  footprints: Footprints,
  scale: Scale,
  pill: Pill,
  'stretch-horizontal': StretchHorizontal,
  dumbbell: Dumbbell,
  moon: Moon,
  'book-open': BookOpen,
  apple: Apple,
  sun: Sun,
};

export const HABIT_ICON_KEYS = Object.keys(HABIT_ICONS);

export function habitIcon(icon: string | null | undefined, type: HabitType): LucideIcon {
  if (icon && HABIT_ICONS[icon]) return HABIT_ICONS[icon];
  if (type === 'Checkbox') return CheckCircle2;
  if (type === 'Numeric') return Hash;
  return Circle;
}
