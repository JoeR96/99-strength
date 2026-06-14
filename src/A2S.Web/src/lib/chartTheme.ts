/**
 * chartTheme — shared colour tokens for Recharts charts.
 *
 * Recharts renders to SVG and cannot consume Tailwind utility classes, so chart
 * colours must be passed as concrete CSS values (stroke/fill strings). We reference
 * the app's theme CSS custom properties directly.
 *
 * IMPORTANT: the theme variables defined in `index.css` are named `--color-*` and
 * already hold a complete colour value (e.g. `--color-primary: hsl(25 80% 45%)`).
 * Therefore the correct reference is `var(--color-primary)` — NOT `hsl(var(--primary))`,
 * which resolves to `hsl(undefined)` (invalid) and falls back to black, rendering the
 * chart illegible on every theme. Use these tokens everywhere instead of inline strings.
 *
 * Because these are `var(...)` references (not snapshot values), charts re-resolve the
 * colour automatically when the active theme class changes — no re-render required.
 */

export const chartColors = {
  /** Primary accent — olive (Retro), gold (OSRS), blue (Apple). Default line/series colour. */
  primary: 'var(--color-primary)',
  /** Translucent primary for area-chart fills (theme primary at 20% over transparent). */
  primaryTranslucent: 'color-mix(in srgb, var(--color-primary) 20%, transparent)',
  /** Secondary accent for charts that genuinely need a second distinct series. */
  accent: 'var(--color-accent)',
  /** Positive/success series. */
  success: 'var(--color-success)',
  /** Negative/destructive series. */
  destructive: 'var(--color-destructive)',
  /** Warning series. */
  warning: 'var(--color-warning)',
  /** Axis ticks, axis lines, and other low-emphasis text. */
  mutedForeground: 'var(--color-muted-foreground)',
  /** Grid lines and chart borders. */
  border: 'var(--color-border)',
  /** Tooltip / surface background. */
  card: 'var(--color-card)',
  /** Default body text colour (tooltip text). */
  foreground: 'var(--color-foreground)',
} as const;

/**
 * Ordered palette for multi-series charts (e.g. comparing several exercises at once).
 * All entries are theme tokens, so series stay on-theme across Retro / OSRS / Apple.
 * Index into it with `chartSeriesPalette[i % chartSeriesPalette.length]`.
 */
export const chartSeriesPalette = [
  chartColors.primary,
  chartColors.accent,
  chartColors.success,
  chartColors.destructive,
  chartColors.warning,
] as const;

/**
 * Shared styling for Recharts <Tooltip contentStyle> so tooltips match the active theme.
 */
export const chartTooltipContentStyle = {
  backgroundColor: chartColors.card,
  border: `1px solid ${chartColors.border}`,
  borderRadius: '8px',
  color: chartColors.foreground,
  fontSize: '13px',
} as const;

/**
 * Shared axis styling (stroke + tick fill) so axis labels are always legible on theme.
 */
export const chartAxisProps = {
  stroke: chartColors.mutedForeground,
  fontSize: 12,
  tick: { fill: chartColors.mutedForeground },
} as const;
