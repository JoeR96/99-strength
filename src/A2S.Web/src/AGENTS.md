# UI & Styling Rules — A2S.Web/src

Scope: everything under `src/`. Inherits `../AGENTS.md` (stack, structure, conventions). This file is the **styling contract** for the single "Arcade Minimal" theme. Read it before adding or changing any colour, font, spacing, or chart.

## The theme

One theme: **Arcade Minimal** — Apple-style minimal design language carrying the Retro Arcade palette. Dark near-black surfaces, burnt-orange primary, neon-yellow accent, system fonts, subtle 1px borders, soft shadows. There is no theme switcher and no `.dark`/`.apple-theme` class; a `dark:` Tailwind variant is always dead code. The ~126 existing occurrences are being removed by the Phase 2 sweep (see Known debt) — never add new ones, and don't hand-remove them ahead of that sweep.

## The token contract

All design tokens live in `index.css` as Tailwind v4 `@theme` custom properties. Each `--color-*` already holds a complete colour value (`--color-primary: hsl(25 80% 45%)`), so the **only** correct references are:

- ✅ Tailwind utilities: `bg-primary`, `text-foreground`, `border-border`, `text-muted-foreground`, `bg-card`, `text-destructive`, …
- ✅ Raw CSS var when a utility can't be used (SVG, inline style): `var(--color-primary)`
- ❌ `hsl(var(--color-primary))` or `hsl(var(--primary))` — double-wrapped → `hsl(undefined)` → black/illegible
- ❌ Literal `#hex` / `hsl(...)` / `rgb(...)` in components — including inline `style`, arbitrary values (`bg-[hsl(...)]`), and constant maps

For translucency use `color-mix(in srgb, var(--color-primary) 20%, transparent)`, never an alpha slot.

Fixed categorical palettes (values that must stay distinct) are centralised: `lib/chartTheme.ts` for chart series, `lib/blockColors.ts` for training-block identity. The `--color-neon-*` tokens exist **for charts/data-viz only** — never for UI chrome.

## Typography

System font stack only (`--font-display` for headings/`font-display` utility, `--font-text` for body). No decorative fonts, no `text-transform: uppercase` for headings, no letterspaced all-caps labels except tiny eyebrow captions.

Type scale (don't invent sizes):

| Role | Use |
|---|---|
| Page title | `.text-hero` (one per page) |
| Section heading | `h2`/`h3` element styles, or `text-xl`/`text-2xl font-semibold` |
| Body | `.text-body` or default body text |
| Caption / secondary | `.text-caption` or `text-sm text-muted-foreground` |

## Spacing & layout

- Page shell: `.container-page` + vertical `py-8` (pages), `space-y-6` between page sections.
- Cards: `p-6` padding (compact lists may use `p-4`), `gap-4` internal stacks.
- Stick to the 4/6/8 Tailwind steps for padding/gaps; anything else needs a reason.

## Surfaces & depth

- Cards/panels: `bg-card border border-border` + `rounded-lg` (tokens give Apple-style radii). Shadows only for elevation that means something (modals `shadow-lg`, popovers `shadow-md`); resting cards need no shadow.
- No glows, scanlines, pixel effects, or gradient backgrounds.

## Charts (Recharts)

Recharts renders SVG and cannot consume Tailwind classes, so colours must be concrete strings. Always import from `lib/chartTheme.ts` — never inline colour strings in a chart:

- Single-series chart (even with a metric switcher): `chartColors.primary` for line, dots, legend.
- Multi-series: `chartSeriesPalette[i % chartSeriesPalette.length]`.
- Always set `tick={{ fill: chartColors.mutedForeground }}` on axes — `stroke` alone leaves tick text illegible.
- Tooltip: `contentStyle={chartTooltipContentStyle}`.

## Components

- ShadCN primitives in `components/ui/` for buttons, cards, dialogs, inputs. Compose these rather than re-styling raw elements.
- `cn()` from `lib/utils` for conditional class merging.
- Max 500 lines/file (see parent `AGENTS.md`).
- Touch targets ≥ 44px on interactive elements.

## Known debt (don't replicate; fix opportunistically)

_Tracked in `docs/superpowers/audits/2026-07-18-frontend-audit-findings.md` during the flagship audit; migrate leftovers here when that effort closes._
