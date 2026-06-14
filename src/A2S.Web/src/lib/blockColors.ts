/**
 * blockColors — canonical colours for training-block identity (Block 1 / 2 / 3).
 *
 * These are a *fixed categorical* palette: a block's colour identifies the block and
 * should stay stable and distinct regardless of the active theme (same rationale as a
 * multi-series chart palette). This is the single source of truth — previously the
 * colours were duplicated and had *diverged* between the history calendar (blue/purple/
 * pink) and the block-sequence editor (blue/amber/red, via Tailwind `dark:` classes that
 * only triggered on the OSRS theme). Import from here instead of redefining inline.
 *
 * Expressed as hex so they can be used both as inline `style` values (calendar cells,
 * badges) and as SVG fills. Block numbers are 1-based.
 */
export const blockColors: Record<number, string> = {
  1: '#3b82f6', // blue
  2: '#8b5cf6', // violet
  3: '#ec4899', // pink
};

/** Colour for a block, falling back to Block 1's colour for unknown block numbers. */
export function getBlockColor(blockNumber: number): string {
  return blockColors[blockNumber] ?? blockColors[1];
}
