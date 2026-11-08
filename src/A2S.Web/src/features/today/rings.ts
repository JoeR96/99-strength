/** Weekly goal ring maths, kept apart from the component for Fast Refresh and tests. */

import { Dumbbell, HeartPulse, StretchHorizontal, Utensils, Wine, type LucideIcon } from 'lucide-react';
import type { WeeklyGoalsDto } from '@/types/plan';

/** 0..1 share of the goal done; nothing planned is 0. */
export function ringProgress(done: number, planned: number): number {
  if (planned <= 0) return 0;
  return Math.min(1, Math.max(0, done / planned));
}

/** Allowances count the other way: the ring fills as you spend them and turns amber when over. */
export function allowanceState(used: number, allowed: number): { progress: number; over: boolean } {
  if (allowed <= 0) return { progress: used > 0 ? 1 : 0, over: used > 0 };
  return { progress: Math.min(1, used / allowed), over: used > allowed };
}

export interface RingDefinition {
  key: string;
  label: string;
  icon: LucideIcon;
  done: number;
  planned: number;
  progress: number;
  tone: 'primary' | 'success' | 'warning' | 'muted';
}

export function ringsFor(week: WeeklyGoalsDto): RingDefinition[] {
  const session = (key: string, label: string, icon: LucideIcon, done: number, planned: number): RingDefinition => {
    const progress = ringProgress(done, planned);
    return {
      key,
      label,
      icon,
      done,
      planned,
      progress,
      tone: planned === 0 ? 'muted' : progress >= 1 ? 'success' : 'primary',
    };
  };
  const allowance = (key: string, label: string, icon: LucideIcon, used: number, allowed: number): RingDefinition => {
    const { progress, over } = allowanceState(used, allowed);
    return { key, label, icon, done: used, planned: allowed, progress, tone: over ? 'warning' : used === 0 ? 'muted' : 'primary' };
  };
  return [
    session('lifts', 'Lifts', Dumbbell, week.liftsDone, week.liftsPlanned),
    session('cardio', 'Cardio', HeartPulse, week.cardioDone, week.cardioPlanned),
    session('mobility', 'Mobility', StretchHorizontal, week.mobilityDone, week.mobilityPlanned),
    allowance('meal', 'Flex meal', Utensils, week.flexibleMealsUsed, week.flexibleMealsAllowed),
    allowance('alcohol', 'Alcohol', Wine, week.alcoholOccasions, week.alcoholAllowed),
  ];
}
