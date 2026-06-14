# UI & Styling Rules — A2S.Web/src

Scope: everything under `src/`. Inherits `../AGENTS.md` (stack, structure, conventions). This file is the **styling and theming contract**. Read it before adding or changing any colour, font, or chart.

## The token contract (read this first)

All theme colours live in `index.css` as Tailwind v4 `@theme` custom properties, named `--color-*`, and **each already holds a complete colour value**:

```css
--color-primary: hsl(25 80% 45%);   /* a full hsl(), not raw channels */
--color-border:  hsl(0 0% 25%);
```

Three themes redefine these on a root class (`ThemeContext` sets it):

| Theme | Root class |
|-------|-----------|
| Retro Arcade | _(none, `:root`)_ |
| OSRS | `.dark` |
| Apple | `.apple-theme` |

Because the value is complete, the **only** correct references are:

- ✅ Tailwind utilities: `bg-primary`, `text-foreground`, `border-border`, `text-muted-foreground`, `bg-card`, `text-destructive`, …
- ✅ Raw CSS var (when a utility can't be used, e.g. SVG/inline style): `var(--color-primary)`

**Never** do these — they produce `hsl(undefined)` → black → illegible on every theme:

- ❌ `hsl(var(--primary))`  (double-wrapped + wrong name; the var is `--color-primary`)
- ❌ `hsl(var(--color-primary))`  (double-wrapped — the var already has `hsl()`)

For translucency, use `color-mix`, not an alpha slot:

- ✅ `color-mix(in srgb, var(--color-primary) 20%, transparent)`
- ❌ `hsl(var(--primary) / 0.2)`

### Don't hardcode colours

Use theme tokens, not literal `#hex` / `hsl(...)` / `rgb(...)` in components — whether as inline `style`, Tailwind arbitrary values (`bg-[hsl(...)]`), or constant maps. A hardcoded colour won't change with the theme and silently breaks one or more of the three themes.

If you genuinely need a **fixed categorical palette** (distinct values that must stay distinct regardless of theme), centralise it in one module — `lib/chartTheme.ts` for chart series, `lib/blockColors.ts` for training-block identity — and import it. Don't redefine palettes inline per file.

### Tailwind `dark:` variant ≠ our themes

Tailwind's `dark:` modifier keys off the `.dark` class — which here means **OSRS only**, not "dark mode" in general (Retro is also dark; Apple is light). Don't reach for `dark:` to mean "dark theme". Style with the semantic tokens, which already resolve per active theme.

## Charts (Recharts)

Recharts renders SVG and **cannot consume Tailwind classes**, so colours must be concrete strings. Always import from **`lib/chartTheme.ts`** — never inline colour strings in a chart:

```tsx
import { chartColors, chartTooltipContentStyle, chartSeriesPalette } from '@/lib/chartTheme';

<CartesianGrid stroke={chartColors.border} />
<XAxis stroke={chartColors.mutedForeground} tick={{ fill: chartColors.mutedForeground }} />
<Tooltip contentStyle={chartTooltipContentStyle} />
<Line stroke={chartColors.primary} />
```

- **Single-series chart** (one metric shown at a time, even with a weight/volume/reps switcher): use `chartColors.primary` for the line, dots, and legend so the series colour matches its label consistently across themes.
- **Multi-series chart** (several series at once): index `chartSeriesPalette[i % chartSeriesPalette.length]` — all entries are theme tokens.
- Always set `tick={{ fill: chartColors.mutedForeground }}` on axes — `stroke` alone colours the axis line but not the tick **text**, which is the usual cause of illegible labels.

## Fonts

Font families are theme tokens too (`--font-display`, `--font-text`, `--font-arcade`), overridden per theme (OSRS → RuneScape UF, Apple → SF Pro). Use the `font-*` utilities / tokens; don't hardcode a `font-family`.

## Components

- ShadCN primitives in `components/ui/` for buttons, cards, dialogs, inputs. Compose these rather than re-styling raw elements.
- `cn()` from `lib/utils` for conditional class merging.
- Max 500 lines/file (see parent `AGENTS.md`).

## Known debt (don't replicate; fix opportunistically)

_None tracked here right now._ The two former entries — `Navbar.tsx`'s hardcoded per-theme `hsl(...)` chains and the duplicated/divergent `BLOCK_COLORS` maps — have been migrated to semantic tokens and `lib/blockColors.ts` respectively. When you spot a hardcoded colour or a `dark:`-to-mean-dark-theme usage, fix it and (if non-trivial) note it here.
