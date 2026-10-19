/**
 * blockColors — canonical colours for training-block identity (Block 1 / 2 / 3).
 *
 * These are a *fixed categorical* palette: a block's colour identifies the block and
 * should stay stable and distinct regardless of the active theme (same rationale as a
 * multi-series chart palette). This is the single source of truth — previously the
 * colours were duplicated and had *diverged* between the history calendar (blue/purple/
 * pink) and the block-sequence editor. Import from here instead of redefining inline.
 *
 * Expressed as hex so they can be used both as inline `style` values (calendar cells,
 * badges) and as SVG fills. Block numbers are 1-based.
 *
 * Contrast (2026-09-29): the 400-weight shades carry dark text. Measured against
 * `--color-background` text (hsl(240 10% 4%)): blue 7.8:1, violet 7.3:1, pink 7.5:1; as text
 * on `--color-card`: blue 7.0:1, violet 6.6:1, pink 6.8:1. The old 500 shades only reached
 * 3.5–4.2:1 under white text.
 */
export const blockColors: Record<number, string> = {
  1: '#60a5fa', // blue
  2: '#a78bfa', // violet
  3: '#f472b6', // pink
};


/** Colour for a block, falling back to Block 1's colour for unknown block numbers. */
export function getBlockColor(blockNumber: number): string {
  return blockColors[blockNumber] ?? blockColors[1];
}
