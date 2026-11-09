/**
 * Shared Clerk `appearance` config for sign-in and sign-up, themed to match
 * the app's Arcade Minimal dark theme.
 *
 * `variables` is the one sanctioned place outside `index.css`/`lib` where
 * literal `hsl()` values are used: Clerk renders its widget in an isolated
 * context that cannot read our Tailwind utility classes or `var(--color-*)`
 * custom properties, so these values are copy-pasted verbatim from the
 * design tokens in `src/index.css`. If those tokens change, update here too.
 */
export const clerkAppearance = {
  variables: {
    colorPrimary: 'hsl(25 80% 50%)', // --color-primary (burnt orange)
    colorBackground: 'hsl(240 10% 10%)', // --color-card
    colorText: 'hsl(0 0% 95%)', // --color-foreground
    colorInputBackground: 'hsl(0 0% 20%)', // --color-input
    colorInputText: 'hsl(0 0% 95%)', // --color-foreground
  },
  elements: {
    rootBox: 'mx-auto w-full',
    card: 'bg-transparent shadow-none p-6',
    headerTitle: 'text-foreground font-bold',
    headerSubtitle: 'text-muted-foreground',
    socialButtonsBlockButton:
      'min-h-11 bg-secondary/50 border-border/50 text-secondary-foreground hover:bg-secondary/70 transition-all',
    // With three or more providers Clerk renders icon-only buttons. Clerk sets their min-height
    // itself (32 px, unlayered CSS), so the 44 px has to be !important to win over it.
    socialButtonsIconButton: 'min-h-11!',
    // GitHub's mark is black and vanished on the dark card.
    socialButtonsProviderIcon__github: 'invert',
    formFieldLabel: 'text-foreground font-medium',
    formFieldInput:
      'min-h-11 bg-muted/50 border-border/50 text-foreground placeholder:text-muted-foreground focus:border-primary focus:ring-primary/20',
    formFieldInputShowPasswordButton: 'min-h-11 min-w-11 justify-center',
    formButtonPrimary:
      'min-h-11 bg-primary text-primary-foreground hover:bg-primary/90 shadow-md hover:shadow-lg transition-all font-semibold',
    // The link is 44 px tall now, so centre the sentence beside it.
    footerAction: 'items-center!',
    footerActionLink: 'inline-flex min-h-11 min-w-11 items-center justify-center text-primary hover:text-primary/80',
    dividerLine: 'bg-border/50',
    dividerText: 'text-muted-foreground',
  },
};
