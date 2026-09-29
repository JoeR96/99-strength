/**
 * Shared Clerk `appearance` config for sign-in and sign-up, themed to match
 * the app's Arcade Minimal dark theme.
 *
 * `variables` is the one sanctioned place outside `index.css`/`lib` where
 * literal `hsl()` values are used: Clerk renders its widget with its own
 * runtime styles and cannot read our `var(--color-*)` custom properties, so
 * these values are copied from the design tokens in `src/index.css`. If those
 * tokens change, update here too.
 *
 * Contrast (WCAG 2.x, measured 2026-09-29 against the widget background
 * `--color-card` hsl(240 10% 10%) unless noted):
 *   - colorForeground hsl(0 0% 95%) (titles, labels, input text)   16.0:1
 *   - colorMutedForeground hsl(0 0% 65%) (subtitle, divider "or",
 *     footer "Don't have an account?", "Secured by Clerk")          7.3:1
 *   - colorPrimary hsl(25 80% 50%) as link text ("Sign up")          5.6:1
 *   - colorPrimaryForeground near-black on the orange button        5.9:1
 *     (Clerk's default white-on-orange was 3.85:1 and failed AA)
 *   - colorDanger hsl(0 100% 62%) (field errors)                    5.1:1
 *   - input text on colorInput hsl(0 0% 20%)                         11.3:1
 *   - colorBorder hsl(0 0% 45%) (input outlines, UI component)       3.8:1 (≥ 3:1)
 */
export const clerkAppearance = {
  variables: {
    colorPrimary: 'hsl(25 80% 50%)', // --color-primary (burnt orange)
    colorPrimaryForeground: 'hsl(240 10% 8%)', // --color-primary-foreground
    colorBackground: 'hsl(240 10% 10%)', // --color-card
    colorForeground: 'hsl(0 0% 95%)', // --color-foreground
    colorMutedForeground: 'hsl(0 0% 65%)', // --color-muted-foreground
    colorMuted: 'hsl(240 10% 16%)', // --color-muted
    colorNeutral: 'hsl(0 0% 95%)', // dark theme: light neutral for hovers/borders
    colorBorder: 'hsl(0 0% 45%)', // lighter than --color-border so inputs clear 3:1
    colorInput: 'hsl(0 0% 20%)', // --color-input
    colorInputForeground: 'hsl(0 0% 95%)', // --color-foreground
    colorDanger: 'hsl(0 100% 62%)', // --color-destructive
    colorSuccess: 'hsl(120 100% 45%)', // --color-success
    colorWarning: 'hsl(50 100% 50%)', // --color-warning
    colorRing: 'hsl(25 80% 50%)', // --color-ring
    colorShadow: 'hsl(0 0% 0%)',
    fontFamily: 'inherit',
    borderRadius: '0.75rem', // --radius-md
  },
  elements: {
    rootBox: 'mx-auto w-full',
    cardBox: 'w-full shadow-none border-0',
    card: 'bg-card shadow-none',
    formButtonPrimary: 'font-semibold',
  },
};
