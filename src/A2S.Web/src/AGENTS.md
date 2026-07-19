# UI & Styling Rules — A2S.Web/src

Scope: everything under `src/`. Inherits `../AGENTS.md` (stack, structure, conventions). This file is the **styling contract** for the single "Arcade Minimal" theme. Read it before adding or changing any colour, font, spacing, or chart.

## The theme

One theme: **Arcade Minimal** — Apple-style minimal design language carrying the Retro Arcade palette. Dark near-black surfaces, burnt-orange primary, neon-yellow accent, system fonts, subtle 1px borders, soft shadows. There is no theme switcher and no `.dark`/`.apple-theme` class; a `dark:` Tailwind variant is always dead code. The Phase 2 audit sweep removed all live occurrences (only a doc-comment prose match in `BlockSequenceEditor.tsx` remains) — never add new ones.

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

The Arcade Minimal audit (`docs/superpowers/audits/2026-07-18-frontend-audit-findings.md`, closed 2026-07-19) resolved every P1 and all but one P2 (`SetupWizard.tsx` over-500-line split, deferred — see below). Genuinely outstanding items a future styling change should know about:

- **`LoginPage.tsx` dead decorative classes** — `bg-gradient-navy`, `text-gradient-gold`, and a scanline `linear-gradient` grid persist (page is still routed). Off-contract retro leftovers; remove if you touch this file.
- **`CardTitle` keeps `tracking-wide`** in `components/ui/card.tsx` — harmless (no ALL-CAPS pairing) but off the type scale; don't copy the letterspacing.
- **`equipmentStyle()` in `lib/muscleGroupStyles.ts` is exported but unwired** (no call sites). `muscleGroupStyle()` is wired.
- **Categorical badge colours cycle a fixed 8-token `--color-neon-*` palette** by index (`lib/muscleGroupStyles.ts`), so muscle groups past the 8th share a colour. Inherent to a fixed palette — don't "fix" by inventing raw colours.
- **`lib/blockColors.ts` holds literal hex** for training-block identity — **sanctioned** by the token contract; leave it.
- **Over-500-line files not split**: `features/workout/SetupWizard.tsx` (608 — Task 14 extracted the template-conversion function to `lib/templateConversion.ts`, but the component itself remains over the cap; step-render blocks are the natural seam for a future split), `features/workout/SimulationPage.tsx` (694, internal dev tool) and the static data tables `data/hevyExercises.ts` (3109) / `data/workoutTemplates.ts` (1093, out of scope for the component line limit).
- **`useSyncExerciseEditsToHevy` / `deriveExerciseEditStates`** (extracted from `EditExercisesModal`) have no dedicated unit tests yet.
- **Lint baseline: 60 pre-existing errors** (`tests/e2e/*` unused vars, `rules-of-hooks`, `Navbar.tsx` setState-in-effect) — toolchain cleanup scheduled separately; don't add to it.
- **`modal-weight-confirm`** was never reachable (needs a completed session) — not visually signed off.
