import type { ReactNode } from 'react';

/**
 * Shared frame for the sign-in and sign-up screens.
 *
 * Phone-first: `min-h-dvh` follows the visible viewport (100vh ignores the iOS keyboard and
 * browser chrome), content is top-aligned on short screens so the keyboard never pushes the
 * Continue button out of reach, and the safe-area insets keep it clear of the status bar and
 * home indicator in the installed app. The background is a single soft glow in the app's own
 * orange, painted inside the viewport, so nothing overflows sideways.
 */
export function AuthLayout({ title, subtitle, hint, footer, children }: {
  title: string;
  subtitle: string;
  hint?: string;
  footer?: string;
  children: ReactNode;
}) {
  return (
    <div
      data-testid="auth-layout"
      className="relative flex min-h-dvh justify-center overflow-x-hidden bg-background text-foreground items-start sm:items-center pt-[max(env(safe-area-inset-top),1.5rem)] pb-[max(env(safe-area-inset-bottom),1.5rem)]"
      style={{
        backgroundImage: 'radial-gradient(ellipse at 50% 0%, hsl(25 80% 50% / 0.12), transparent 60%)',
      }}
    >
      <main className="w-full max-w-md px-4">
        <div className="mb-6 text-center">
          <div className="mb-4 flex justify-center" aria-hidden="true">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl border border-primary/20 bg-primary/10">
              <span className="text-3xl font-black text-primary">99</span>
            </div>
          </div>
          <h1 className="text-3xl font-black tracking-tight text-foreground sm:text-4xl">{title}</h1>
          <p className="mt-2 text-base font-medium text-foreground/90">{subtitle}</p>
          {hint && <p className="mt-1 text-sm text-muted-foreground">{hint}</p>}
        </div>

        <div className="rounded-2xl border border-primary/10 bg-card p-1 shadow-2xl shadow-black/20">{children}</div>

        {footer && <p className="mt-6 text-center text-xs text-muted-foreground">{footer}</p>}
      </main>
    </div>
  );
}
