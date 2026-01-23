import { SignIn } from '@clerk/clerk-react';

/**
 * Login page component that displays Clerk's pre-built sign-in UI with SSO options.
 * Uses Golden Twilight theme with navy background and gold accents.
 */
export function LoginPage() {
  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-gradient-navy">
      {/* Background decorative elements */}
      <div className="absolute inset-0 overflow-hidden">
        <div className="absolute -top-1/2 -left-1/4 h-[800px] w-[800px] rounded-full bg-primary/5 blur-3xl" />
        <div className="absolute -bottom-1/2 -right-1/4 h-[600px] w-[600px] rounded-full bg-primary/10 blur-3xl" />
        <div className="absolute top-1/4 right-1/4 h-[300px] w-[300px] rounded-full bg-primary/5 blur-2xl" />
      </div>

      {/* Grid pattern overlay */}
      <div
        className="absolute inset-0 opacity-[0.02]"
        style={{
          backgroundImage: `linear-gradient(rgba(255,212,10,0.3) 1px, transparent 1px),
                           linear-gradient(90deg, rgba(255,212,10,0.3) 1px, transparent 1px)`,
          backgroundSize: '50px 50px',
        }}
      />

      <div className="relative z-10 w-full max-w-md px-4">
        {/* Hero Section */}
        <div className="mb-10 text-center">
          <div className="mb-6 flex justify-center">
            <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-primary/10 backdrop-blur-sm border border-primary/20 shadow-[0_0_40px_rgba(255,212,10,0.2)]">
              <span className="text-4xl font-black text-primary">A</span>
            </div>
          </div>
          <h1 className="text-5xl font-black tracking-tight text-gradient-gold drop-shadow-lg">
            A2S Tracker
          </h1>
          <p className="mt-4 text-lg text-gold-light/80 font-medium">
            Track your strength journey
          </p>
          <p className="mt-2 text-sm text-muted-foreground">
            Sign in to continue to your dashboard
          </p>
        </div>

        {/* Sign In Card */}
        <div className="rounded-2xl border border-primary/10 bg-card/80 backdrop-blur-xl p-1 shadow-2xl shadow-black/20">
          <SignIn
            routing="path"
            path="/sign-in"
            signUpUrl="/sign-up"
            fallbackRedirectUrl="/dashboard"
            appearance={{
              elements: {
                rootBox: "mx-auto w-full",
                card: "bg-transparent shadow-none p-6",
                headerTitle: "text-foreground font-bold",
                headerSubtitle: "text-muted-foreground",
                socialButtonsBlockButton:
                  "bg-secondary/50 border-border/50 text-secondary-foreground hover:bg-secondary/70 transition-all",
                formFieldLabel: "text-foreground font-medium",
                formFieldInput:
                  "bg-muted/50 border-border/50 text-foreground placeholder:text-muted-foreground focus:border-primary focus:ring-primary/20",
                formButtonPrimary:
                  "bg-primary text-primary-foreground hover:bg-primary/90 shadow-md hover:shadow-lg transition-all font-semibold",
                footerActionLink: "text-primary hover:text-primary/80",
                dividerLine: "bg-border/50",
                dividerText: "text-muted-foreground",
              },
            }}
          />
        </div>

        {/* Footer */}
        <p className="mt-8 text-center text-xs text-muted-foreground/60">
          Built for strength athletes. Powered by Average to Savage 2.0
        </p>
      </div>
    </div>
  );
}
