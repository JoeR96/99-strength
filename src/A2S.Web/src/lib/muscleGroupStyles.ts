/**
 * muscleGroupStyles — canonical colours for muscle-group / equipment badge identity.
 *
 * These are a *fixed categorical* palette: a muscle group's (or equipment type's)
 * colour identifies it and should stay stable and distinct, independent of the active
 * theme — same rationale as `blockColors.ts` and a multi-series chart palette. The
 * styling contract (`src/AGENTS.md`) permits `--color-neon-*` tokens for this kind of
 * categorical data identity (never for general UI chrome).
 *
 * Previously each muscle group had a bespoke, off-token Tailwind colour class string
 * (`text-orange-600`, `bg-orange-500/10`, …) hardcoded in `ExerciseLibraryComponents.tsx`.
 * This file centralises that mapping and expresses it as `var(--color-neon-*)`-based
 * inline `style` objects instead, cycling through the fixed neon palette by index so
 * every group/equipment stays visually distinct without inventing new raw colours.
 */

/** Ordered, fixed categorical palette. Index into it with `i % neonPalette.length`. */
const neonPalette = [
  'var(--color-neon-cyan)',
  'var(--color-neon-magenta)',
  'var(--color-neon-yellow)',
  'var(--color-neon-green)',
  'var(--color-neon-orange)',
  'var(--color-neon-pink)',
  'var(--color-neon-purple)',
  'var(--color-neon-blue)',
] as const;

/** Badge style: translucent tint background, translucent tint border, solid text colour. */
export interface CategoricalBadgeStyle {
  color: string;
  backgroundColor: string;
  borderColor: string;
}

function badgeStyleFor(colour: string): CategoricalBadgeStyle {
  return {
    color: colour,
    backgroundColor: `color-mix(in srgb, ${colour} 10%, transparent)`,
    borderColor: `color-mix(in srgb, ${colour} 30%, transparent)`,
  };
}

/**
 * Stable ordering of muscle groups, used to assign palette indices. Order matches the
 * original bespoke colour map so the visual "feel" (which groups share/neighbour a hue)
 * stays as close as possible to before, even though the underlying values changed.
 */
const MUSCLE_GROUP_ORDER = [
  'abdominals',
  'adductors',
  'back',
  'biceps',
  'calves',
  'cardio',
  'chest',
  'forearms',
  'full_body',
  'glutes',
  'hamstrings',
  'lats',
  'lower_back',
  'neck',
  'obliques',
  'other',
  'quadriceps',
  'shoulders',
  'traps',
  'triceps',
] as const;

const EQUIPMENT_ORDER = [
  'barbell',
  'bodyweight',
  'cable',
  'dumbbell',
  'ez_bar',
  'kettlebell',
  'machine',
  'none',
  'other',
  'plate',
  'resistance_band',
  'smith_machine',
  'suspension',
  'trap_bar',
] as const;

const muscleGroupIndex: Record<string, number> = Object.fromEntries(
  MUSCLE_GROUP_ORDER.map((group, i) => [group, i])
);

const equipmentIndex: Record<string, number> = Object.fromEntries(
  EQUIPMENT_ORDER.map((equipment, i) => [equipment, i])
);

const fallbackStyle = badgeStyleFor('var(--color-muted-foreground)');

/** Categorical badge style for a muscle group. Falls back to a neutral style for unknown groups. */
export function muscleGroupStyle(group: string): CategoricalBadgeStyle {
  const index = muscleGroupIndex[group];
  if (index === undefined) return fallbackStyle;
  return badgeStyleFor(neonPalette[index % neonPalette.length]);
}

/** Categorical badge style for an equipment type. Falls back to a neutral style for unknown types. */
export function equipmentStyle(equipment: string): CategoricalBadgeStyle {
  const index = equipmentIndex[equipment];
  if (index === undefined) return fallbackStyle;
  return badgeStyleFor(neonPalette[index % neonPalette.length]);
}
