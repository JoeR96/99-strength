# Theme System

This project uses a CSS custom properties-based theming system with shadcn/ui components. All components should exclusively use shadcn/ui components and the defined color palette.

## Golden Twilight Palette

The default theme is "Golden Twilight" - midnight navy and sapphire set off sparkling yellows, capturing the intrigue of nightfall's gold.

### Colors

- **Ink Black** (`#000814`) - Ultra-dark with a hint of blue
- **Prussian Blue** (`#001d3d`) - Inky, profound blue filled with gravitas
- **Oxford Navy** (`#003566`) - Deep, dignified blue reminiscent of midnight sky
- **School Bus Yellow** (`#ffc300`) - Dynamic, iconic golden yellow
- **Gold** (`#ffd60a`) - Radiant golden spark shimmer

## Usage in Components

### Using Semantic Color Classes

Always prefer semantic color classes that work with both light and dark modes:

```tsx
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";

function MyComponent() {
  return (
    <Card className="bg-card text-card-foreground">
      <Button variant="default">Primary Action</Button>
      <Button variant="secondary">Secondary Action</Button>
      <Button variant="destructive">Delete</Button>
    </Card>
  );
}
```

### Available Semantic Colors

- `bg-background` / `text-foreground` - Main background and text
- `bg-card` / `text-card-foreground` - Cards and elevated surfaces
- `bg-primary` / `text-primary-foreground` - Primary actions (Gold)
- `bg-secondary` / `text-secondary-foreground` - Secondary actions (Oxford Navy)
- `bg-accent` / `text-accent-foreground` - Accent elements (School Bus Yellow)
- `bg-muted` / `text-muted-foreground` - Muted/disabled states
- `border-border` - Borders and dividers
- `ring-ring` - Focus rings

### Using Palette Colors Directly

For custom designs, you can access the palette colors directly:

```tsx
<div className="bg-ink-black text-gold">
  <h1>Direct palette usage</h1>
</div>
```

Available classes:
- `bg-ink-black` / `text-ink-black`
- `bg-prussian-blue` / `text-prussian-blue`
- `bg-oxford-navy` / `text-oxford-navy`
- `bg-school-bus-yellow` / `text-school-bus-yellow`
- `bg-gold` / `text-gold`

## Theme Switching

The theme system supports easy palette switching:

```tsx
import { applyTheme, toggleThemeMode, goldenTwilight } from "@/themes";

// Apply Golden Twilight in light mode
applyTheme(goldenTwilight, "light");

// Apply Golden Twilight in dark mode
applyTheme(goldenTwilight, "dark");

// Toggle between light and dark
toggleThemeMode();
```

## Adding New Themes

To add a new color palette:

1. Create a new theme file in `src/themes/` (e.g., `ocean-breeze.ts`)
2. Follow the same structure as `golden-twilight.ts`
3. Export it from `src/themes/index.ts`
4. All components will automatically work with the new theme

Example:

```typescript
// src/themes/ocean-breeze.ts
export const oceanBreeze = {
  name: "Ocean Breeze",
  colors: {
    // Define your palette
  },
  light: {
    background: "...",
    foreground: "...",
    // ... etc
  },
  dark: {
    // ... dark mode variants
  },
} as const;
```

## Best Practices

1. **Always use shadcn/ui components** - Never create custom styled components from scratch
2. **Use semantic colors** - Prefer `bg-primary` over `bg-gold` for consistency
3. **Support both modes** - Test components in both light and dark mode
4. **Don't hardcode colors** - Use the theme system instead of hex values
5. **Use the `cn()` utility** - For combining and merging Tailwind classes

```tsx
import { cn } from "@/lib/utils";

<div className={cn("bg-card", isActive && "border-2 border-primary")} />
```
