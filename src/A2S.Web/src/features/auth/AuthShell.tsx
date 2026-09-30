import type { ReactNode } from 'react';

/**
 * Page shell for sign-in and sign-up: brand column + the Clerk widget in a card.
 * Arcade Minimal tokens only (near-black background, burnt-orange accent).
 *
 * Contrast on --color-background hsl(240 10% 4%) (measured 2026-09-29):
 *   title text-foreground 17.8:1 · body/captions text-muted-foreground 8.1:1 ·
 *   "99" near-black on bg-primary 5.9:1 · text-primary accents 6.3:1.
 */
const FEATURES = [
  'Training maxes adjust after every AMRAP set.',
  'Accessories add sets, then weight, as you hit the top of the range.',
  'Each week syncs to Hevy as ready-to-log routines.',
];

export function AuthShell({ subtitle, children }: { subtitle: string; children: ReactNode }) {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <main className="container-page flex min-h-screen flex-col justify-center gap-10 py-10 lg:grid lg:grid-cols-2 lg:items-center lg:gap-16">
        <section aria-labelledby="auth-title" className="mx-auto w-full max-w-md lg:mx-0">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-md bg-primary">
              <span className="font-display text-xl font-bold text-primary-foreground">99</span>
            </div>
            <span className="text-caption">Average to Savage 2.0</span>
          </div>
          <h1 id="auth-title" className="text-hero mt-6">
            99 Strength
          </h1>
          <p className="mt-3 text-base text-muted-foreground">{subtitle}</p>
          <ul className="mt-8 hidden space-y-3 lg:block">
            {FEATURES.map((feature) => (
              <li key={feature} className="flex gap-3 text-sm text-muted-foreground">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-hidden="true" />
                {feature}
              </li>
            ))}
          </ul>
        </section>

        <div className="mx-auto w-full max-w-md">
          {children}
          <p className="text-caption mt-6 text-center">Built for strength athletes. Runs the A2S 2.0 program.</p>
        </div>
      </main>
    </div>
  );
}
